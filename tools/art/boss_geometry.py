"""Editable organic construction helpers for the two siege-boss art studies.

Run with Blender Python. Dimensions here are art proportions, never unit balance.
"""
import math
import random

import bpy
import bmesh
from mathutils import Vector


def make_material(name, dark, light, scale=3, roughness=.73, bump=.08):
    """Build self-contained mottled materials without external texture downloads."""
    material = bpy.data.materials.new(name)
    material.diffuse_color = (*light, 1)
    material.use_nodes = True
    nodes, links = material.node_tree.nodes, material.node_tree.links
    shader = nodes.get('Principled BSDF')
    shader.inputs['Roughness'].default_value = roughness
    shader.inputs['Specular IOR Level'].default_value = .28
    coordinates = nodes.new('ShaderNodeTexCoord')
    noise = nodes.new('ShaderNodeTexNoise')
    noise.inputs['Scale'].default_value = scale
    noise.inputs['Detail'].default_value = 5
    noise.inputs['Roughness'].default_value = .73
    links.new(coordinates.outputs['Object'], noise.inputs['Vector'])
    ramp = nodes.new('ShaderNodeValToRGB')
    ramp.color_ramp.elements[0].position = .23
    ramp.color_ramp.elements[0].color = (*dark, 1)
    ramp.color_ramp.elements[1].position = .78
    ramp.color_ramp.elements[1].color = (*light, 1)
    links.new(noise.outputs['Fac'], ramp.inputs[0])
    links.new(ramp.outputs[0], shader.inputs['Base Color'])
    pores = nodes.new('ShaderNodeTexNoise')
    pores.inputs['Scale'].default_value = scale * 16
    pores.inputs['Detail'].default_value = 3
    links.new(coordinates.outputs['Object'], pores.inputs['Vector'])
    relief = nodes.new('ShaderNodeBump')
    relief.inputs['Strength'].default_value = .33
    relief.inputs['Distance'].default_value = bump
    links.new(pores.outputs['Fac'], relief.inputs['Height'])
    links.new(relief.outputs['Normal'], shader.inputs['Normal'])
    return material


def shade_surface(obj, material):
    if material:
        obj.data.materials.append(material)
    if obj.type == 'MESH':
        for face in obj.data.polygons:
            face.use_smooth = True
    return obj


def make_volume(name, position, radius, material=None, direction=None):
    """Elliptical anatomical mass; later fused into the continuous body."""
    bpy.ops.mesh.primitive_uv_sphere_add(segments=24, ring_count=16, location=position)
    obj = bpy.context.object
    obj.name = name
    obj.scale = radius
    if direction is not None:
        obj.rotation_euler = Vector(direction).to_track_quat('Z', 'Y').to_euler()
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    return shade_surface(obj, material)


def make_muscle(name, start, end, width, depth=None, material=None):
    start, end = Vector(start), Vector(end)
    return make_volume(name, (start+end)/2,
                       (width, depth or width, (end-start).length*.66),
                       material, end-start)


def make_curve(name, points, radius, material, cyclic=False):
    curve = bpy.data.curves.new(name, 'CURVE')
    curve.dimensions = '3D'
    curve.resolution_u = 5
    curve.bevel_depth = radius
    curve.bevel_resolution = 3
    spline = curve.splines.new('BEZIER')
    spline.bezier_points.add(len(points)-1)
    spline.use_cyclic_u = cyclic
    for point, coordinate in zip(spline.bezier_points, points):
        point.co = coordinate
        point.handle_left_type = 'AUTO'
        point.handle_right_type = 'AUTO'
    obj = bpy.data.objects.new(name, curve)
    bpy.context.collection.objects.link(obj)
    return shade_surface(obj, material)


def fuse_volumes(parts, name, material, voxel_size=.025):
    """Apply union remesh and smoothing; retain an editable single skin surface."""
    bpy.ops.object.select_all(action='DESELECT')
    for obj in parts:
        obj.select_set(True)
    bpy.context.view_layer.objects.active = parts[0]
    bpy.ops.object.join()
    obj = bpy.context.object
    obj.name = name
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
    remesh = obj.modifiers.new('Continuous anatomical union', 'REMESH')
    remesh.mode = 'VOXEL'
    remesh.voxel_size = voxel_size
    remesh.use_smooth_shade = True
    bpy.ops.object.modifier_apply(modifier=remesh.name)
    smooth = obj.modifiers.new('Soften muscle transitions', 'SMOOTH')
    smooth.factor = .85
    smooth.iterations = 4
    bpy.ops.object.modifier_apply(modifier=smooth.name)
    obj.data.materials.clear()
    shade_surface(obj, material)
    texture = bpy.data.textures.new(name+' | broad skin relief', 'CLOUDS')
    texture.noise_scale = .12
    texture.noise_depth = 2
    displace = obj.modifiers.new('Editable subtle skin relief', 'DISPLACE')
    displace.texture = texture
    displace.strength = .010
    displace.mid_level = .5
    return obj


def carve_recess(body, name, center, radii):
    """Cut a real eye/mouth recess instead of placing cartoon patches on the skin."""
    cutter = make_volume(name+' cutter', center, radii)
    bpy.context.view_layer.objects.active = body
    modifier = body.modifiers.new(name, 'BOOLEAN')
    modifier.operation = 'DIFFERENCE'
    modifier.solver = 'EXACT'
    modifier.object = cutter
    while body.modifiers.find(modifier.name) > 0:
        bpy.ops.object.modifier_move_up(modifier=modifier.name)
    bpy.ops.object.modifier_apply(modifier=modifier.name)
    bpy.data.objects.remove(cutter, do_unlink=True)


def remove_micro_fragments(body):
    """Remove only tiny boolean debris; substantial disconnected anatomy is an error."""
    mesh = bmesh.new()
    mesh.from_mesh(body.data)
    remaining = set(mesh.verts)
    fragments = []
    while remaining:
        stack = [remaining.pop()]
        component = []
        while stack:
            vertex = stack.pop()
            component.append(vertex)
            for edge in vertex.link_edges:
                neighbor = edge.other_vert(vertex)
                if neighbor in remaining:
                    remaining.remove(neighbor)
                    stack.append(neighbor)
        if len(component) <= 16:
            diameter = max((a.co-b.co).length for a in component for b in component)
            if diameter < .08:
                fragments.extend(component)
    removed = len(fragments)
    if fragments:
        bmesh.ops.delete(mesh,geom=fragments,context='VERTS')
        mesh.to_mesh(body.data)
    mesh.free()
    body['removed_boolean_debris_vertices'] = removed
    return removed


def make_hand(parts, name, wrist, forward, scale=1):
    """Connected palm, four curved fingers, and an opposed thumb, all fused later."""
    wrist, forward = Vector(wrist), Vector(forward).normalized()
    across = forward.cross(Vector((0, 1, 0))).normalized()
    if across.length < .1:
        across = Vector((1, 0, 0))
    palm = wrist+forward*.20*scale
    parts.append(make_volume(name+' palm', palm, (.20*scale, .14*scale, .27*scale), direction=forward))
    for index in range(4):
        root = palm+across*((index-1.5)*.098*scale)+forward*.12*scale
        middle = root+forward*((.23-abs(index-1.5)*.028)*scale)
        tip = middle+forward*.12*scale+Vector((0, .09*scale, 0))
        parts.append(make_muscle(name+' finger proximal', root, middle, .064*scale))
        parts.append(make_muscle(name+' finger distal', middle, tip, .052*scale))
    root = palm-across*.16*scale-forward*.07*scale
    middle = root-across*.13*scale+forward*.10*scale
    parts.append(make_muscle(name+' thumb base', root, middle, .080*scale))
    parts.append(make_muscle(name+' thumb tip', middle, middle+forward*.14*scale, .059*scale))


def make_torn_cloth(name, center, radii, top, length, material, seed=1):
    """Sewn draped tube with asymmetrical torn hem and open weave-sized holes."""
    rng = random.Random(seed)
    columns, rows = 80, 18
    lengths = [length*(.75+rng.random()*.35) for _ in range(columns)]
    vertices, faces = [], []
    for row in range(rows+1):
        t = row/rows
        for column in range(columns):
            angle = column/columns*math.tau
            flutter = .035*math.sin(angle*11+t*7)+t*.025*math.sin(angle*23)
            vertices.append((center[0]+(radii[0]*(1+.20*t)+flutter)*math.cos(angle),
                             center[1]+(radii[1]*(1+.22*t)+flutter)*math.sin(angle),
                             top-t*lengths[column]))
    for row in range(rows):
        for column in range(columns):
            # Tears restricted to the skirt, not random cuts through structural skin.
            if row > rows*.65 and (column*7+row*3+seed) % 43 < 2:
                continue
            nxt = (column+1) % columns
            faces.append((row*columns+column, row*columns+nxt,
                          (row+1)*columns+nxt, (row+1)*columns+column))
    mesh = bpy.data.meshes.new(name)
    mesh.from_pydata(vertices, [], faces)
    mesh.update()
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(obj)
    shade_surface(obj, material)
    solidify = obj.modifiers.new('Fabric thickness', 'SOLIDIFY')
    solidify.thickness = .015
    return obj


def make_band(name, points, width, material):
    """Create a thickened fabric strip following a sampled path."""
    vertices = []
    for index, point in enumerate(points):
        point = Vector(point)
        tangent = Vector(points[min(index+1, len(points)-1)])-Vector(points[max(index-1, 0)])
        across = tangent.cross(Vector((0, 1, 0))).normalized()*width/2
        vertices.extend([point-across, point+across])
    mesh = bpy.data.meshes.new(name)
    mesh.from_pydata(vertices, [], [(i*2, i*2+1, i*2+3, i*2+2) for i in range(len(points)-1)])
    mesh.update()
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(obj)
    shade_surface(obj, material)
    modifier = obj.modifiers.new('Supple cloth', 'SUBSURF')
    modifier.levels = 2
    modifier = obj.modifiers.new('Cloth edge thickness', 'SOLIDIFY')
    modifier.thickness = .022
    return obj


def make_tattered_strip(name, points, width, material, seed):
    """Asymmetric broad cloth with folds and torn edges, not a smooth leather strap."""
    rng = random.Random(seed)
    rows, columns = 32, 10
    points = [Vector(point) for point in points]
    vertices, faces = [], []
    for row in range(rows+1):
        t = row/rows
        segment = min(int(t*(len(points)-1)),len(points)-2)
        blend = t*(len(points)-1)-segment
        center = points[segment].lerp(points[segment+1],blend)
        tangent = (points[segment+1]-points[segment]).normalized()
        across = tangent.cross(Vector((0,1,0))).normalized()
        thickness = tangent.cross(across)
        extent = width*(.8+.14*math.sin(row*2.1)+rng.random()*.12)
        for column in range(columns+1):
            u = column/columns
            point = center+across*(u-.5)*extent
            point += thickness*.026*math.sin(u*math.tau*3+row*.37)
            if row > rows-3:
                point += tangent*.14*math.sin(column*2.9+seed)*(row-rows+3)/3
            vertices.append(point)
    for row in range(rows):
        for column in range(columns):
            if row>rows*.45 and (row*11+column*7+seed)%73<3:
                continue
            offset=row*(columns+1)+column
            faces.append((offset,offset+1,offset+columns+2,offset+columns+1))
    mesh=bpy.data.meshes.new(name)
    mesh.from_pydata(vertices,[],faces)
    mesh.update()
    obj=bpy.data.objects.new(name,mesh)
    bpy.context.collection.objects.link(obj)
    shade_surface(obj,material)
    solidify=obj.modifiers.new('Frayed fabric thickness','SOLIDIFY')
    solidify.thickness=.012
    return obj


def make_wood(name, start, end, radius, material):
    start, end = Vector(start), Vector(end)
    bpy.ops.mesh.primitive_cylinder_add(vertices=10, radius=radius, depth=(end-start).length,
                                       location=(start+end)/2)
    obj = bpy.context.object
    obj.name = name
    obj.rotation_euler = (end-start).to_track_quat('Z', 'Y').to_euler()
    bevel = obj.modifiers.new('Worn timber edges', 'BEVEL')
    bevel.width = .026
    bevel.segments = 2
    return shade_surface(obj, material)


def make_rope_wrap(name, start, end, radius, turns, material):
    start, end = Vector(start), Vector(end)
    axis = (end-start).normalized()
    across = axis.cross(Vector((0, 1, 0))).normalized()
    if across.length < .1:
        across = Vector((1, 0, 0))
    other = axis.cross(across)
    points = []
    for index in range(turns*24+1):
        t = index/(turns*24)
        angle = t*turns*math.tau
        points.append(start.lerp(end, t)+radius*(across*math.cos(angle)+other*math.sin(angle)))
    return make_curve(name, points, .022, material)
