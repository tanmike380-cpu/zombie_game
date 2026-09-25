"""Reversible, mitten-like RTS grips; no articulated finger or nail detail."""
import math

import bpy
from mathutils import Vector

from human_hands import get_hand_basis, get_rest_joints
from hand_contacts import build_stock_surface, get_stock_distance, get_barrel_distance, find_clear_contact

HAND_PREFIX = 'RTS Hand | '
MASK_GROUP = 'RTS | keep body without detailed hands'


def mask_detailed_hands(body):
    """Keep original topology and shape keys; hide only distal hand surfaces."""
    group = body.vertex_groups.new(name=MASK_GROUP)
    kept = []
    for vertex in body.data.vertices:
        world = body.matrix_world @ vertex.co
        side = -1 if world.x < 0 else 1
        distance = (world-get_rest_joints(side)[2]).dot(get_hand_basis(side)[0])
        if not (abs(world.x) > .33 and .73 < world.z < .99 and distance > .018):
            kept.append(vertex.index)
    group.add(kept, 1, 'REPLACE')
    modifier = body.modifiers.new(MASK_GROUP, 'MASK')
    modifier.vertex_group = group.name
    modifier.threshold = .5


def add_loft(parts, name, centers, axes, radii):
    """Closed smooth tube with elliptical sections, later fused into one hand."""
    vertices, faces = [], []
    segments = 12
    for center, (first, second), (width, depth) in zip(centers, axes, radii):
        for index in range(segments):
            angle = math.tau*index/segments
            vertices.append(center+first*(width*math.cos(angle))+second*(depth*math.sin(angle)))
    for ring in range(len(centers)-1):
        for index in range(segments):
            a = ring*segments+index
            b = ring*segments+(index+1)%segments
            faces.append((a, b, b+segments, a+segments))
    faces.extend([tuple(reversed(range(segments))), tuple((len(centers)-1)*segments+i for i in range(segments))])
    mesh = bpy.data.meshes.new(name)
    mesh.from_pydata(vertices, [], faces)
    mesh.update()
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(obj)
    parts.append(obj)


def add_capsule(parts, name, start, end, radius, width=None):
    direction = (end-start).normalized()
    across = direction.cross(Vector((0, 0, 1)))
    if across.length < .01:
        across = direction.cross(Vector((1, 0, 0)))
    across.normalize()
    normal = direction.cross(across).normalized()
    centers = [start-direction*radius*.65, start, end, end+direction*radius*.65]
    radii = [(radius*.12, radius*.12), (width or radius, radius),
             (width or radius, radius), (radius*.12, radius*.12)]
    add_loft(parts, name, centers, [(across, normal)]*4, radii)


def make_grip(preset, role, side, surface, inverse):
    """A fused C grip plus one short thumb; trigger hand adds an index silhouette."""
    axis = Vector(preset['gun_axis']).normalized()
    lateral = Vector((0, 0, 1)).cross(axis).normalized()
    up = axis.cross(lateral).normalized()
    grip_distance = preset['grip_distance_m'][role]-(.045 if role == 'trigger' else 0)
    center = Vector(preset['gun_origin'])+axis*grip_distance-up*.023
    rig = bpy.data.objects['Equipment | editable holding pose rig']
    wrist = rig.matrix_world @ rig.pose.bones[f'hand.{side}'].head
    # The opening must face the barrel, not the camera-facing side of the wood.
    # This leaves visible pads around both sides and the underside of the fore-end.
    radial = -up
    tangent = axis.cross(radial).normalized()
    parts, centers, axes, radii = [], [], [], []
    for index in range(25):
        angle = math.radians(-125+250*index/24)
        outward = radial*math.cos(angle)+tangent*math.sin(angle)
        hit = surface.ray_cast(center, outward, .13)[0]
        radius = (hit-center).length if hit is not None else .024
        centers.append(center+outward*(radius+.011))
        axes.append((axis, outward))
        taper = .35 if index in (0, 24) else (.8 if index in (1, 23) else 1)
        radii.append((.030*taper, .012*taper))
    add_loft(parts, 'mitten curl', centers, axes, radii)
    attach = centers[12]
    forearm = rig.pose.bones[f'forearm.{side}']
    arm_axis = (forearm.tail-forearm.head).normalized()
    add_capsule(parts, 'palm', wrist-arm_axis*.045, attach, .026, .029)
    # A modest thumb lobe overlaps the palm instead of looking like a separate claw.
    thumb_base = centers[18]+axis*(-side*.027)
    thumb_tip = centers[22]+axis*(-side*.026)
    add_capsule(parts, 'thumb', thumb_base, thumb_tip, .010)
    if role == 'trigger':
        origin = Vector(preset['gun_origin'])
        points = [origin+axis*x+lateral*y+up*z for x, y, z in
                  ((.245,-.048,-.026),(.300,-.050,-.028),(.323,-.043,-.045),
                   (.315,-.027,-.061),(.295,-.022,-.061))]
        for start, end in zip(points, points[1:]):
            add_capsule(parts, 'curved trigger index', start, end, .009)
    bpy.ops.object.select_all(action='DESELECT')
    for obj in parts:
        obj.select_set(True)
    bpy.context.view_layer.objects.active = parts[0]
    bpy.ops.object.join()
    hand = parts[0]
    hand.name = HAND_PREFIX+role
    remesh = hand.modifiers.new('Fuse palm and mitten silhouette', 'REMESH')
    remesh.mode = 'VOXEL'
    remesh.voxel_size = .0035
    bpy.ops.object.modifier_apply(modifier=remesh.name)
    smooth = hand.modifiers.new('Round silhouette', 'SMOOTH')
    smooth.factor = .7
    smooth.iterations = 4
    bpy.ops.object.modifier_apply(modifier=smooth.name)
    for vertex in hand.data.vertices:
        if min(get_stock_distance(vertex.co, surface), get_barrel_distance(vertex.co, inverse)) < .0005:
            vertex.co = find_clear_contact(vertex.co, surface, inverse)
    hand.data.materials.append(bpy.data.materials['HumanBase | warm skin'])
    color = hand.data.color_attributes.new(name='HumanSkin', type='FLOAT_COLOR', domain='POINT')
    for entry in color.data:
        entry.color = (.43, .265, .165, 1)
    for face in hand.data.polygons:
        face.use_smooth = True
    # Preserve the posed world mesh while attaching it rigidly to the existing hand bone.
    world = hand.matrix_world.copy()
    hand.parent = rig
    hand.parent_type = 'BONE'
    hand.parent_bone = f'hand.{side}'
    hand.matrix_world = world
    hand['hand_style'] = 'C mitten + thumb' if side > 0 else 'C mitten + thumb + pointing index'
    return hand


def measure_rts_hands(body, inverse):
    """Validate the visible replacement, not the hidden anatomical fingers."""
    modifier = body.modifiers.get(MASK_GROUP)
    if not modifier or not modifier.show_render or not modifier.show_viewport:
        raise ValueError('Detailed hand mask missing or disabled')
    surface = build_stock_surface()
    report = {}
    for role in ('trigger', 'support'):
        hand = bpy.data.objects[HAND_PREFIX+role]
        points = [hand.matrix_world @ vertex.co for vertex in hand.data.vertices]
        distances = [min(get_stock_distance(point, surface), get_barrel_distance(point, inverse)) for point in points]
        triangles = sum(len(face.vertices)-2 for face in hand.data.polygons)
        if triangles > 15000 or hand.parent_type != 'BONE':
            raise ValueError(f'Invalid RTS hand topology/binding: {role}')
        report[role] = {'vertices': len(points), 'triangles': triangles,
                        'deep_intersections': sum(value < -.003 for value in distances),
                        'minimum_clearance_mm': round(min(distances)*1000, 3)}
    return report


def build_rts_hands(body, preset, inverse):
    mask_detailed_hands(body)
    surface = build_stock_surface()
    for side, role in ((-1, 'trigger'), (1, 'support')):
        make_grip(preset, role, side, surface, inverse)
    bpy.context.view_layer.update()
    return measure_rts_hands(body, inverse)
