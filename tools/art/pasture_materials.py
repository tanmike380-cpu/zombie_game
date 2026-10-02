"""Crop-only overlay for the raised Tripo field; source architecture and textures are immutable."""
import math
import random

import bpy
from mathutils import Vector
from mathutils.bvhtree import BVHTree


def add_ribbon(vertices, faces, shades, points, widths, angle, shade):
    """Append a curved tapered leaf or stem instead of a straight needle."""
    start = len(vertices)
    side = Vector((math.cos(angle), math.sin(angle), 0))
    for point, width in zip(points, widths):
        vertices.extend((point-side*width, point+side*width))
    for index in range(len(points)-1):
        offset = start+index*2
        faces.append((offset, offset+1, offset+3, offset+2))
        shades.append(shade)


def add_seed_head(vertices, faces, shades, base, angle, shade):
    """Add a short, plump golden ear readable from the game camera."""
    start = len(vertices)
    for level, radius in enumerate((.0007, .0022, .0020, .0002)):
        center = base+Vector((.0015*(level/3)**2, 0, level*.0024))
        for side in range(4):
            theta = angle+side*math.pi/2
            vertices.append(center+Vector((math.cos(theta)*radius, math.sin(theta)*radius, 0)))
    for level in range(3):
        for side in range(4):
            faces.append((start+level*4+side, start+level*4+(side+1)%4,
                          start+(level+1)*4+(side+1)%4, start+(level+1)*4+side))
            shades.append(shade+side%2)


def add_crop_stalk(vertices, faces, shades, base, rng, height):
    """Layer a bent stem, two leaves and a seed head within the existing raised bed."""
    angle = rng.uniform(0, math.tau)
    bend = Vector((rng.uniform(.002,.008), rng.uniform(.001,.006), 0))
    top = base+bend+Vector((0,0,height))
    add_ribbon(vertices, faces, shades, (base,base+bend*.3+Vector((0,0,height*.55)),top),
               (.0005,.0004,.0002),angle,rng.randrange(2))
    for index in range(2):
        leaf_angle = angle+index*math.pi
        direction = Vector((math.cos(leaf_angle),math.sin(leaf_angle),0))
        root = base+Vector((0,0,height*(.2+index*.2)))
        mid = root+direction*.005+Vector((0,0,height*.3))
        tip = root+direction*.011+Vector((0,0,height*.12))
        add_ribbon(vertices, faces, shades,(root,mid,tip),(.0003,.0017,.00002),
                   leaf_angle+math.pi/2,1+rng.randrange(3))
    add_seed_head(vertices,faces,shades,top,angle,4+rng.randrange(3))


def is_crop_surface(hit, normal):
    """Include raised bed shoulders, but reject low paths, roofs and fence faces."""
    if hit is None or normal.z <= .25:
        return False
    main_bed = -.38 < hit.x < .18 and -.35 < hit.y < .27 and .025 < hit.z < .075
    front_bed = -.27 < hit.x < .08 and -.42 < hit.y < -.35 and .025 < hit.z < .075
    return main_bed or front_bed


def add_straw_cover(obj):
    """Cover raised crop surfaces only, with one independently removable mesh."""
    rng = random.Random(2042)
    tree = BVHTree.FromPolygons([v.co for v in obj.data.vertices], [list(p.vertices) for p in obj.data.polygons])
    vertices, faces, shades, roots = [], [], [], []
    slope_count = 0
    for row in range(113):
        for column in range(91):
            x = -.38+column*.0062+rng.uniform(-.002,.002)
            y = -.42+row*.0062+rng.uniform(-.002,.002)
            hit, normal, _, _ = tree.ray_cast(Vector((x,y,.6)), Vector((0,0,-1)))
            if not is_crop_surface(hit, normal):
                continue
            # Closely spaced planted rows, with gently correlated canopy height.
            slope = normal.z < .95 or hit.z < .071
            if slope and rng.random() < .12:
                continue
            height = (.014 if slope else .024)+.003*math.sin(x*32+y*16)+rng.uniform(-.003,.003)
            slope_count += int(slope)
            add_crop_stalk(vertices,faces,shades,hit+Vector((0,0,.0004)),rng,height)
            roots.append(tuple(hit))
    if not roots:
        raise ValueError("No raised field detected; inspect this source mesh before changing the crop mask")
    mesh = bpy.data.meshes.new("Raised field crop — leafy golden ears")
    mesh.from_pydata(vertices, [], faces)
    mesh.update()
    straw = bpy.data.objects.new("Golden straw cover — editable separate layer", mesh)
    bpy.context.scene.collection.objects.link(straw)
    palette = ((.17,.15,.027),(.26,.23,.042),(.34,.30,.055),(.43,.36,.074),
               (.45,.30,.050),(.55,.39,.080),(.65,.47,.120),(.73,.55,.170))
    for index, color in enumerate(palette):
        material = bpy.data.materials.new(f"Crop natural tone {index}")
        material.diffuse_color = (*color,1)
        material.use_nodes = True
        nodes, links = material.node_tree.nodes, material.node_tree.links
        emission = nodes.new("ShaderNodeEmission")
        emission.inputs["Color"].default_value = (*color,1)
        output = next(node for node in nodes if node.type == "OUTPUT_MATERIAL")
        links.new(emission.outputs[0],output.inputs["Surface"])
        mesh.materials.append(material)
    for polygon, shade in zip(mesh.polygons,shades):
        polygon.material_index = shade
    straw["stalk_count"] = len(roots)
    straw["crop_only"] = True
    straw["slope_stalk_count"] = slope_count
    straw["root_z_range"] = [min(p[2] for p in roots),max(p[2] for p in roots)]
    return straw
