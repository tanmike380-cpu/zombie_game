"""Build two original Blender siege-zombie studies from the approved concepts.

Usage: blender --background --python tools/art/boss_models.py -- --boss all
This authoring script does not write Unity, balance, or the approved human master.
"""
import argparse
import hashlib
import html
import json
import logging
import math
from pathlib import Path
import sys

import bpy
from mathutils import Matrix, Vector

sys.path.insert(0, str(Path(__file__).resolve().parent))
from boss_geometry import (make_material, make_volume, make_muscle, make_curve,
                           fuse_volumes, make_hand, make_torn_cloth, make_band,
                           make_wood, make_rope_wrap, carve_recess, make_tattered_strip,
                           remove_micro_fragments)

ROOT = Path(__file__).resolve().parents[2]
OUTPUT = ROOT/'Builds/ArtReview/BossModels-v1'
REFERENCE = ROOT/'Builds/ArtReview/RosterConcepts-2026-09-27/04-zombies-06-07.png'
LOGGER = logging.getLogger('boss_models')


def build_materials():
    return {
        'skin': make_material('Skin | grey brown corpse mottling', (.055,.046,.033), (.29,.245,.18), 3.6, .64, .010),
        'bone': make_material('Forehead | dense callused bone', (.095,.078,.040), (.34,.28,.16), 5, .70, .014),
        'dark': make_material('Creases | deep umber', (.018,.014,.009), (.065,.045,.025), 8, .85, .004),
        'eye': make_material('Eyes | small dark sunken eyes', (.040,.038,.027), (.13,.125,.083), 15, .51, .001),
        'tooth': make_material('Teeth | old ivory', (.20,.15,.07), (.62,.52,.30), 9, .55, .007),
        'cloth': make_material('Cloth | faded madder rags', (.038,.021,.016), (.19,.082,.043), 8, .94, .014),
        'cloth_dark': make_material('Cloth | charcoal hemp', (.025,.030,.025), (.105,.11,.077), 9, .95, .012),
        'rope': make_material('Rope | weathered hemp', (.085,.060,.029), (.29,.21,.108), 14, .91, .009),
        'wood': make_material('Timber | rough scavenged poles', (.042,.026,.013), (.19,.115,.050), 4, .83, .02),
    }


def build_legs(parts, is_carrier):
    """Thigh/knee/calf/ankle transition without separate visible ball joints."""
    for side in (-1, 1):
        hip = (side*.56, .13, 2.0 if is_carrier else 1.73)
        knee = (side*(.83 if is_carrier else .99), -.39 if side < 0 else -.18, 1.06 if is_carrier else .91)
        ankle = (side*(.94 if is_carrier else 1.14), .09 if side < 0 else .34, .29)
        parts.append(make_muscle('Thigh', hip, knee, .42 if is_carrier else .49, .45))
        parts.append(make_volume('Knee', knee, (.27,.29,.29)))
        parts.append(make_muscle('Calf', knee, ankle, .30, .31))
        parts.append(make_volume('Ankle', ankle, (.20,.24,.24)))
        foot = (ankle[0], ankle[1]-.25, .17)
        parts.append(make_volume('Broad weight-bearing foot', foot, (.34,.48,.18)))
        for toe in range(5):
            parts.append(make_volume('Toe', (foot[0]+(toe-2)*.115, foot[1]-.36, .145),
                                     (.078,.19-abs(toe-1)*.016,.103)))


def build_torso(parts, is_carrier):
    masses = [
        ('Pelvis', (0,.18,1.97), (.78,.52,.60)),
        ('Abdomen', (0,-.01,2.58), (.75,.57,.75)),
        ('Rib cage', (0,-.04,3.16), (1.03,.67,.78)),
        ('Hunched back', (0,.36,3.48), (.88,.66,.70)),
        ('Trapezius', (0,-.12,3.83), (.79,.58,.48)),
    ]
    for name, point, radius in masses:
        if is_carrier:
            point = (point[0],point[1],point[2]+.16)
            radius = (radius[0]*.83,radius[1]*.83,radius[2])
        parts.append(make_volume(name, point, radius))
    for side in (-1,1):
        parts.append(make_volume('Pectoral plane', (side*.40,-.52,3.25), (.46,.17,.38)))
        parts.append(make_muscle('Neck tendon', (side*.48,-.02,3.82),
                                 (side*.23,-.95,3.65 if is_carrier else 3.29), .26, .29))


def build_arms(parts, is_carrier, materials):
    for side in (-1,1):
        shoulder = Vector((side*1.05,-.10,3.44+(side*.05 if not is_carrier else 0)))
        if is_carrier and side == 1:
            shoulder = Vector((.83,-.10,3.63))
            elbow, wrist = Vector((1.35,.20,5.02)), Vector((2.00,-.38,4.57))
            forward = (.12,-.04,-1)
        else:
            elbow = Vector((side*1.35,-.46,2.51))
            wrist = Vector((side*1.43,-1.02,1.60 if is_carrier else 1.75+side*.10))
            forward = (side*.04,-.22,-1)
        width = .28 if is_carrier else .39
        parts.append(make_volume('Deltoid', shoulder, (width*1.35,width*1.3,width*1.45)))
        parts.append(make_muscle('Biceps and triceps', shoulder, elbow, width, width*.91))
        parts.append(make_volume('Elbow connection', elbow, (width*.74,)*3))
        parts.append(make_muscle('Forearm flexors', elbow, wrist, width*.75, width*.70))
        parts.append(make_volume('Wrist bridge', wrist, (width*.47,)*3))
        make_hand(parts, 'Hand '+str(side), wrist, forward, .94 if is_carrier else 1.04)
        if not (is_carrier and side == 1):
            start = wrist.lerp(elbow,.10)
            end = wrist.lerp(elbow,.31)
            make_rope_wrap('Wrist binding '+str(side), start, end, width*.62, 4, materials['rope'])


def build_head(parts, is_carrier, materials):
    """Build skull, angled brows, sunken eyes and teeth without an armor helmet."""
    center, size = get_head_parameters(is_carrier)
    def pos(x,y,z):
        return center+Vector((x,y,z))*size
    parts.append(make_volume('Skull anatomy', center, (.46*size,.49*size,.57*size)))
    parts.append(make_volume('Heavy jaw', pos(0,-.12,-.34), (.31*size,.35*size,.25*size)))
    parts.append(make_volume('Nose bridge', pos(0,-.47,-.03), (.10*size,.15*size,.20*size)))
    for side in (-1,1):
        parts.append(make_muscle('Angled brow', pos(side*.065,-.43,.14), pos(side*.36,-.38,.21), .105*size))
        parts.append(make_volume('Cheek plane', pos(side*.32,-.31,-.18), (.16*size,.22*size,.20*size)))
        parts.append(make_volume('Ear', pos(side*.47,.005,-.08), (.08*size,.14*size,.16*size)))
        make_volume('Clouded eye', pos(side*.203,-.409,.02), (.029*size,.027*size,.020*size), materials['eye'])
        make_volume('Nostril', pos(side*.069,-.587,-.123), (.031*size,.018*size,.025*size), materials['dark'])
    make_volume('Mouth recess', pos(0,-.340,-.315), (.225*size,.032*size,.099*size), materials['dark'])
    for index in range(9):
        for row in (-1,1):
            if (index+row) % 4 == 0:
                continue
            point = pos((index-4)*.046,-.406+abs(index-4)*.004,-.315+row*.049)
            make_volume('Broken tooth', point, (.016*size,.022*size,(.031+index%3*.004)*size), materials['tooth'])
    if not is_carrier:
        # Fuse the cranial shield into the body: no separate helmet-like cap seam.
        forehead = make_volume('Forehead | impact callus', pos(0,.005,.32),(.57,.64,.59))
        for vertex in forehead.data.vertices:
            for axis,radius in enumerate((.57,.64,.59)):
                normalized = vertex.co[axis]/radius
                vertex.co[axis] = math.copysign(abs(normalized)**.83,normalized)*radius
        parts.append(forehead)
    return center


def get_head_parameters(is_carrier):
    return (Vector((0,-1.0,3.66)),.76) if is_carrier else (Vector((0,-1.25,3.16)),1.05)


def refine_skin(body, is_carrier, materials):
    """Secondary facial forms and broad corpse coloration, after the union stage."""
    center, size = get_head_parameters(is_carrier)
    for side in (-1,1):
        carve_recess(body,'Anatomical eye socket',center+Vector((side*.203,-.447,.02))*size,
                     (.083*size,.10*size,.062*size))
    carve_recess(body,'Open jaw cavity',center+Vector((0,-.45,-.315))*size,
                 (.242*size,.12*size,.095*size))
    remove_micro_fragments(body)
    skin = materials['skin']
    shader = skin.node_tree.nodes.get('Principled BSDF')
    shader.inputs['Subsurface Weight'].default_value = .035
    ramp = next(node for node in skin.node_tree.nodes if node.type == 'VALTORGB')
    ramp.color_ramp.elements[0].position = .34
    ramp.color_ramp.elements[1].position = .68
    middle = ramp.color_ramp.elements.new(.49)
    middle.color = (.15,.125,.090,1)
    if not is_carrier:
        attribute = body.data.attributes.new('cranial_callus','FLOAT','POINT')
        for vertex,item in zip(body.data.vertices,attribute.data):
            point = body.matrix_world @ vertex.co
            # Broad continuous material transition, no pasted cap or small stitches.
            front = max(0,min(1,(-point.y-1.08)/.45))
            upper = max(0,min(1,(point.z-3.20)/.35))
            lateral = max(0,min(1,(.65-abs(point.x))/.18))
            item.value = front*upper*lateral
        nodes,links = skin.node_tree.nodes,skin.node_tree.links
        mask = nodes.new('ShaderNodeAttribute')
        mask.attribute_name = 'cranial_callus'
        mix = nodes.new('ShaderNodeMixRGB')
        bone_ramp = nodes.new('ShaderNodeValToRGB')
        bone_ramp.color_ramp.elements[0].color = (.14,.11,.065,1)
        bone_ramp.color_ramp.elements[1].color = (.43,.35,.23,1)
        bone_ramp.color_ramp.elements[0].position = .30
        bone_ramp.color_ramp.elements[1].position = .65
        noise = next(node for node in nodes if node.type == 'TEX_NOISE')
        links.new(noise.outputs['Fac'],bone_ramp.inputs[0])
        links.new(bone_ramp.outputs[0],mix.inputs[2])
        links.new(mask.outputs['Fac'],mix.inputs[0])
        links.new(ramp.outputs[0],mix.inputs[1])
        links.new(mix.outputs[0],shader.inputs['Base Color'])
    for side in (-1,1):
        # Restrained folds along cheek/jaw, not an outline around the eyeball.
        make_curve('Cheek | nasolabial fold',
                   [center+Vector((side*.12,-.495,-.13))*size,
                    center+Vector((side*.20,-.447,-.22))*size,
                    center+Vector((side*.26,-.39,-.34))*size], .012*size,materials['skin'])


def build_costume(is_carrier, materials):
    make_torn_cloth('Waist | tattered madder robe', (0,.13), (.79,.60), 2.30, 1.05, materials['cloth'], 17)
    belt = [(math.cos(i/36*math.tau)*.83,.13+math.sin(i/36*math.tau)*.64,2.24+.05*math.sin(i)) for i in range(36)]
    for offset in (0,.07):
        make_curve('Waist rope', [(x,y,z+offset) for x,y,z in belt], .045, materials['rope'], True)
    for side in (-1,1):
        make_tattered_strip('Shoulder | torn mantle',
                  [(side*.87,-.18,3.94),(side*.91,-.65,3.55),
                   (side*.62,-.70,2.94),(side*.40,-.66,2.30),
                   (side*.50,-.78,1.74)], .48 if side<0 else .29,
                   materials['cloth'] if side<0 else materials['cloth_dark'],side+17)
        make_tattered_strip('Back | hanging robe strip',
                  [(side*.75,.40,4.02),(side*.87,.93,3.54),(side*.84,.89,2.70),
                   (side*.90,.72,1.88),(side*.89,.76,1.12)],.57,materials['cloth'],side+21)


def build_passenger(name, center, materials, lean=0):
    """Small ADULT infected, not a child; independently editable mesh and cloth."""
    existing = set(bpy.context.scene.objects)
    center = Vector(center)
    parts = []
    def pos(x,y,z):
        return center+Vector((x+lean*z,y,z))
    parts.append(make_volume(name+' chest',pos(0,0,.72),(.20,.13,.30)))
    parts.append(make_volume(name+' pelvis',pos(0,0,.40),(.15,.13,.16)))
    parts.append(make_muscle(name+' neck',pos(0,0,.88),pos(0,-.03,1.07),.07))
    parts.append(make_volume(name+' head',pos(0,-.035,1.15),(.10,.12,.15)))
    for side in (-1,1):
        for label,start,end,width in [
            ('thigh',pos(side*.10,0,.40),pos(side*.13,-.03,.14),.073),
            ('shin',pos(side*.13,-.03,.14),pos(side*.14,-.04,-.10),.055),
            ('upper arm',pos(side*.19,0,.85),pos(side*.28,-.08,.64),.065),
            ('forearm',pos(side*.28,-.08,.64),pos(side*.29,-.18,.43),.05)]:
            parts.append(make_muscle(name+label,start,end,width))
        parts.append(make_volume(name+' foot',pos(side*.14,-.10,-.10),(.066,.12,.053)))
        parts.append(make_volume(name+' hand',pos(side*.29,-.18,.41),(.055,.055,.075)))
        make_volume(name+' eye shadow',pos(side*.039,-.146,1.17),(.023,.007,.016),materials['dark'])
    make_volume(name+' mouth',pos(0,-.148,1.08),(.045,.018,.025),materials['dark'])
    body = fuse_volumes(parts,name+' | adult anatomy',materials['skin'],.022)
    body['role'] = 'adult passenger, not runtime spawn'
    make_torn_cloth(name+' | rags', (center.x,center.y),(.17,.15),center.z+.58,.40,materials['cloth_dark'],23)
    make_band(name+' | torn shoulder cloth',
              [pos(-.16,.06,.94),pos(-.16,-.15,.79),pos(.04,-.16,.55),pos(.09,-.14,.38)],
              .17,materials['cloth'])
    angle = math.radians(-24 if name.startswith('Grasped') else lean*500)
    pivot = center+Vector((0,0,.85))
    rotation = Matrix.Translation(pivot) @ Matrix.Rotation(angle,4,'Y') @ Matrix.Translation(-pivot)
    for obj in set(bpy.context.scene.objects)-existing:
        obj.matrix_world = rotation @ obj.matrix_world


def build_backpack(materials):
    for side in (-1,1):
        make_wood('Back rack | uprights',(side*.82,.82,2.50),(side*.85,1.04,5.38),.085,materials['wood'])
    for height in (2.77,3.55,4.24,4.95):
        make_wood('Back rack | cross rail',(-.96,1.04,height),(.96,1.04,height+.04),.065,materials['wood'])
        for side in (-1,1):
            make_rope_wrap('Back rack | lashed joint',(side*.83,.99,height-.10),(side*.83,1.01,height+.10),.113,4,materials['rope'])
    for side in (-1,1):
        make_wood('Back rack | depth support',(side*.82,.73,2.58),(side*.82,1.15,3.0),.075,materials['wood'])
        make_curve('Load-bearing harness',[(side*.78,1.0,4.05),(side*.80,-.11,4.10),
                                          (side*.65,-.72,3.52),(side*.20,-.63,2.41),
                                          (side*.74,.80,2.66)],.06,materials['rope'])
    for index in range(3):
        build_passenger('Back passenger '+str(index+1),((index-1)*.53,.89,4.05+(index%2)*.17),materials, .015*(index-1))
    build_passenger('Grasped passenger',(1.99,-.35,3.68),materials,.07)


def create_light(name, location, energy, size, color, target):
    bpy.ops.object.light_add(type='AREA', location=location)
    light = bpy.context.object
    light.name = name
    light.data.energy, light.data.shape, light.data.size = energy, 'DISK', size
    light.data.color = color
    light.rotation_euler = (Vector(target)-light.location).to_track_quat('-Z','Y').to_euler()


def configure_studio():
    scene = bpy.context.scene
    scene.render.engine = 'CYCLES'
    scene.cycles.samples = 24
    scene.cycles.use_denoising = True
    scene.world = bpy.data.worlds.new('Review | studio environment')
    scene.world.use_nodes = True
    scene.world.node_tree.nodes['Background'].inputs[0].default_value = (.20,.22,.25,1)
    scene.world.node_tree.nodes['Background'].inputs[1].default_value = .30
    scene.view_settings.view_transform = 'AgX'
    bpy.ops.mesh.primitive_plane_add(size=200)
    floor = bpy.context.object
    floor.name = 'Review | floor'
    floor.data.materials.append(make_material('Review | warm neutral ground',(.17,.15,.12),(.20,.18,.15),2,.9,.001))
    create_light('Review | warm key',(-4,-6,9),1450,5,(1,.87,.74),(0,0,2.6))
    create_light('Review | cool fill',(5,-1,6),650,5,(.79,.85,1),(0,0,2.6))
    create_light('Review | rim',(1,5,8),2100,4,(1,.85,.63),(0,0,3))
    bpy.ops.object.camera_add()
    scene.camera = bpy.context.object
    scene.camera.name = 'Review | camera'
    scene.camera.data.type = 'ORTHO'
    scene.render.image_settings.file_format = 'PNG'


def configure_camera(is_carrier, view, resolution):
    target = Vector((.28,0,2.7 if is_carrier else 2.15))
    yaw, pitch = {'overview':(-35,16),'rts':(-40,46),'rear':(145,23)}[view]
    yaw, pitch = math.radians(yaw), math.radians(pitch)
    camera = bpy.context.scene.camera
    camera.location = target+Vector((math.sin(yaw)*math.cos(pitch),-math.cos(yaw)*math.cos(pitch),math.sin(pitch)))*16
    camera.rotation_euler = (target-camera.location).to_track_quat('-Z','Y').to_euler()
    camera.data.ortho_scale = 6.75 if is_carrier else 5.8
    scene = bpy.context.scene
    scene.render.resolution_x = resolution
    scene.render.resolution_y = resolution
    scene.render.resolution_percentage = 100
    for screen in bpy.data.screens:
        for area in screen.areas:
            if area.type == 'VIEW_3D':
                area.spaces.active.region_3d.view_perspective = 'CAMERA'
                area.spaces.active.shading.type = 'MATERIAL'


def validate_model(body, is_carrier):
    """Check skin continuity, finite coordinates, support contact, and semantic parts."""
    mesh = body.data
    adjacent = [[] for _ in mesh.vertices]
    for edge in mesh.edges:
        a,b = edge.vertices
        adjacent[a].append(b)
        adjacent[b].append(a)
    remaining = set(range(len(mesh.vertices)))
    components = []
    while remaining:
        stack = [remaining.pop()]
        count = 0
        while stack:
            index = stack.pop()
            count += 1
            for other in adjacent[index]:
                if other in remaining:
                    remaining.remove(other)
                    stack.append(other)
        components.append(count)
    if len(components) != 1:
        raise ValueError(f'Body disconnected: {sorted(components, reverse=True)}')
    assert all(math.isfinite(value) for vertex in mesh.vertices for value in vertex.co)
    depsgraph = bpy.context.evaluated_depsgraph_get()
    triangles = 0
    for obj in bpy.context.scene.objects:
        if obj.type == 'MESH' and not obj.name.startswith('Review'):
            evaluated = obj.evaluated_get(depsgraph)
            sampled = evaluated.to_mesh()
            sampled.calc_loop_triangles()
            triangles += len(sampled.loop_triangles)
            evaluated.to_mesh_clear()
    names = [obj.name for obj in bpy.context.scene.objects]
    assert any('Mouth recess' in name for name in names)
    if is_carrier:
        assert any('Grasped passenger' in name for name in names), 'Missing held passenger'
    else:
        assert 'cranial_callus' in body.data.attributes, 'Missing continuous forehead mask'
    return {'body_components':len(components),'skin_vertices':len(mesh.vertices),
            'removed_boolean_debris_vertices':body.get('removed_boolean_debris_vertices',0),
            'evaluated_mesh_triangles':triangles,'rigged':False,'runtime_ready':False,
            'note':'Static sculpt study; curves are editable. No UV/LOD/animation promise.'}


def organize_collections():
    """Keep editable source categories obvious to someone learning Blender."""
    labels = ('01 Body and face','02 Cloth and bindings','03 Back rack and passengers','04 Review stage')
    collections = {}
    for label in labels:
        collection=bpy.data.collections.new(label)
        bpy.context.scene.collection.children.link(collection)
        collections[label]=collection
    for obj in list(bpy.context.scene.objects):
        name=obj.name.lower()
        if name.startswith('review'):
            label=labels[3]
        elif any(part in name for part in ('passenger','rack','harness')):
            label=labels[2]
        elif any(part in name for part in ('cloth','mantle','robe','rope','binding')):
            label=labels[1]
        else:
            label=labels[0]
        for collection in list(obj.users_collection):
            collection.objects.unlink(obj)
        collections[label].objects.link(obj)


def write_review():
    """Generate a local gallery of actual Blender renders, not AI illustrations."""
    cards=[]
    for kind,title,description in (
        ('headbutter','06 · 头槌巨尸','厚额、低头前倾、耗血撞墙。无手持锤。'),
        ('carrier','07 · 背负投尸巨尸','驼背、木架、长投掷臂；小尸作为破墙投射物。')):
        if not (OUTPUT/kind/(kind+'.blend')).exists():
            continue
        views=''.join(f'<a href="{kind}/{view}.png"><img src="{kind}/{view}.png" alt="{html.escape(title)} {label}"><span>{label}</span></a>'
                      for view,label in (('overview','斜前方'),('rts','RTS 斜俯视'),('rear','背面')))
        cards.append(f'<section><h2>{title}</h2><p>{description}</p><div class="views">{views}</div><p><a href="{kind}/{kind}.blend">可编辑 Blender 文件</a> · <a href="{kind}/verification.json">网格检查记录</a></p></section>')
    page='''<!doctype html><html lang="zh-CN"><meta charset="utf-8"><meta name="viewport" content="width=device-width, initial-scale=1">
<title>攻城 Boss · Blender 模型评审</title><style>
body{margin:0;background:#1c1b18;color:#e6dfd1;font:16px/1.7 system-ui,sans-serif}main{max-width:1500px;margin:auto;padding:32px}
h1{font-size:30px}h2{font-size:24px}p{color:#bbb3a6}a{color:#dfbb77}section{margin:34px 0;padding:20px;background:#27251f;border:1px solid #494135;border-radius:12px}
.views{display:grid;grid-template-columns:repeat(3,1fr);gap:14px}.views img{width:100%;border-radius:8px}.views a{text-decoration:none}.views span{display:block;text-align:center}
aside{padding:16px;border-left:3px solid #ac8750;background:#302b22}@media(max-width:800px){.views{grid-template-columns:1fr}main{padding:16px}}
</style><main><h1>攻城 Boss · Blender 静态模型 v1</h1><p>真实 Cycles 渲染，非概念图替代。点击图片看原尺寸；重点评审 RTS 轮廓、灰褐腐皮、破布和大体型。</p>
<aside>静态可编辑初稿，尚未绑定、重拓扑、烘焙或接入 Unity。不要把本轮网格密度当作游戏性能标准。</aside>'''
    page+=''.join(cards)+'</main></html>'
    (OUTPUT/'review.html').write_text(page,encoding='utf-8')


def build_candidate(kind, resolution, render):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    is_carrier = kind == 'carrier'
    materials = build_materials()
    parts = []
    build_torso(parts,is_carrier)
    build_legs(parts,is_carrier)
    build_arms(parts,is_carrier,materials)
    build_head(parts,is_carrier,materials)
    body = fuse_volumes(parts,'Body | continuous '+kind,materials['skin'])
    refine_skin(body,is_carrier,materials)
    build_costume(is_carrier,materials)
    if is_carrier:
        build_backpack(materials)
    report = validate_model(body,is_carrier)
    configure_studio()
    organize_collections()
    output = OUTPUT/kind
    output.mkdir(parents=True,exist_ok=True)
    configure_camera(is_carrier,'overview',resolution)
    bpy.context.scene['art_status'] = 'Static review v1; not deployed in Unity'
    bpy.context.scene['concept_reference'] = str(REFERENCE)
    bpy.context.scene['boss_mechanic'] = 'Throw adult zombies at walls' if is_carrier else 'Spend own health to headbutt walls'
    bpy.ops.object.select_all(action='DESELECT')
    body.select_set(True)
    bpy.context.view_layer.objects.active = body
    path = output/(kind+'.blend')
    bpy.ops.wm.save_as_mainfile(filepath=str(path),compress=True)
    report['blend_sha256'] = hashlib.sha256(path.read_bytes()).hexdigest()
    (output/'verification.json').write_text(json.dumps(report,indent=2)+'\n')
    if render:
        for view in ('overview','rts','rear'):
            configure_camera(is_carrier,view,resolution)
            bpy.context.scene.render.filepath = str(output/(view+'.png'))
            bpy.ops.render.render(write_still=True)
    LOGGER.info('Completed %s: %s',kind,report)


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--boss',choices=('all','headbutter','carrier'),default='all')
    parser.add_argument('--resolution',type=int,default=900)
    parser.add_argument('--no-render',action='store_true')
    args = parser.parse_args(sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else [])
    logging.basicConfig(level=logging.INFO)
    for kind in ('headbutter','carrier') if args.boss=='all' else (args.boss,):
        build_candidate(kind,args.resolution,not args.no_render)
    write_review()


if __name__ == '__main__':
    main()
