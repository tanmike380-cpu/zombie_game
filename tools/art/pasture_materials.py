"""Native Blender straw/wood materials for the imported untextured pasture."""
import math
import random

import bpy
from mathutils import Vector
from mathutils.bvhtree import BVHTree


def choose_surface_color(center, normal):
    """Provisional architectural palette; source geometry remains unchanged."""
    x, y, z = center
    if z < .043:
        return (.23, .145, .048, 1)
    if abs(x) > .35 and z > .12 and (abs(y) > .30 or x > .40):
        return (.12, .20, .055, 1)
    if x > .15 and z > .16 and normal.z > .25:
        return (.35, .16, .06, 1)
    if x > .15 and z > .07:
        return (.54, .40, .22, 1)
    if z > .13 and abs(normal.z) < .3 and abs(x) < .3:
        return (.32, .075, .08, 1)
    return (.25, .13, .048, 1)


def color_pasture(obj):
    """Add per-corner base colour and directional fibre noise as editable shader nodes."""
    mesh = obj.data
    colors = mesh.color_attributes.new(name="PasturePalette", type="FLOAT_COLOR", domain="CORNER")
    for polygon in mesh.polygons:
        center = sum((mesh.vertices[i].co for i in polygon.vertices), Vector()) / len(polygon.vertices)
        color = choose_surface_color(center, polygon.normal)
        for loop_index in polygon.loop_indices:
            colors.data[loop_index].color = color
    material = bpy.data.materials.new("Pasture — earth, timber and straw")
    material.use_nodes = True
    nodes, links = material.node_tree.nodes, material.node_tree.links
    nodes.clear()
    output = nodes.new("ShaderNodeOutputMaterial")
    emission = nodes.new("ShaderNodeEmission")
    attribute = nodes.new("ShaderNodeVertexColor")
    attribute.layer_name = colors.name
    coordinates = nodes.new("ShaderNodeTexCoord")
    stretch = nodes.new("ShaderNodeVectorMath")
    stretch.operation = "MULTIPLY"
    stretch.inputs[1].default_value = (170, 22, 65)
    noise = nodes.new("ShaderNodeTexNoise")
    noise.inputs["Scale"].default_value = 1
    noise.inputs["Detail"].default_value = 2
    ramp = nodes.new("ShaderNodeValToRGB")
    ramp.color_ramp.elements[0].color = (.48,.48,.48,1)
    ramp.color_ramp.elements[1].color = (1,1,1,1)
    multiply = nodes.new("ShaderNodeMixRGB")
    multiply.blend_type = "MULTIPLY"
    multiply.inputs[0].default_value = 1
    # Contact shading is a material detail for the previously untextured mesh;
    # it adds no lamps and cannot change the user's scene lighting.
    occlusion = nodes.new("ShaderNodeAmbientOcclusion")
    occlusion.inputs["Distance"].default_value = .10
    occlusion.samples = 16
    links.new(coordinates.outputs["Generated"], stretch.inputs[0])
    links.new(stretch.outputs["Vector"], noise.inputs["Vector"])
    links.new(noise.outputs["Fac"], ramp.inputs["Fac"])
    links.new(attribute.outputs["Color"], multiply.inputs[1])
    links.new(ramp.outputs["Color"], multiply.inputs[2])
    links.new(multiply.outputs["Color"], occlusion.inputs["Color"])
    links.new(occlusion.outputs["Color"], emission.inputs["Color"])
    links.new(emission.outputs[0], output.inputs["Surface"])
    mesh.materials.clear()
    mesh.materials.append(material)
    return material


def add_straw_cover(obj):
    """Fill the empty inner field with merged, varied straw blades following source ground."""
    rng = random.Random(2042)
    tree = BVHTree.FromPolygons([v.co for v in obj.data.vertices], [list(p.vertices) for p in obj.data.polygons])
    vertices, faces, shades = [], [], []
    # Review crop patch: stop inside fences and before the farmhouse on the east side.
    for row in range(70):
        for column in range(52):
            x = -.36 + column*.009 + rng.uniform(-.004,.004)
            y = -.35 + row*.010 + rng.uniform(-.004,.004)
            hit, normal, _, _ = tree.ray_cast(Vector((x,y,.6)), Vector((0,0,-1)))
            if hit is None or hit.z > .085 or normal.z < .3:
                continue
            height = rng.uniform(.025,.068)
            angle = rng.uniform(0, math.tau)
            width = rng.uniform(.0013,.0025)
            bend = Vector((rng.uniform(-.009,.012),rng.uniform(.004,.015),height))
            tangent = Vector((math.cos(angle)*width,math.sin(angle)*width,0))
            base = hit+Vector((0,0,.001))
            start = len(vertices)
            vertices.extend((base-tangent, base+tangent, base+bend*.65+tangent*.5,
                             base+bend*.65-tangent*.5, base+bend))
            faces.extend(((start,start+1,start+2,start+3),(start+3,start+2,start+4)))
            shade = rng.randrange(5)
            shades.extend((shade, min(4,shade+1)))
    mesh = bpy.data.meshes.new("Continuous straw patch — merged blades")
    mesh.from_pydata(vertices, [], faces)
    straw = bpy.data.objects.new("Golden straw cover — editable separate layer", mesh)
    bpy.context.scene.collection.objects.link(straw)
    palette = ((.26,.19,.055),(.38,.28,.085),(.49,.37,.12),(.60,.47,.18),(.68,.56,.27))
    for index, color in enumerate(palette):
        material = bpy.data.materials.new(f"Straw fibre {index}")
        material.diffuse_color = (*color,1)
        material.use_nodes = True
        nodes = material.node_tree.nodes
        shader = nodes.new("ShaderNodeEmission")
        shader.inputs["Color"].default_value = (*color,1)
        output = next(node for node in nodes if node.type == "OUTPUT_MATERIAL")
        material.node_tree.links.new(shader.outputs[0], output.inputs["Surface"])
        mesh.materials.append(material)
    for polygon, shade in zip(mesh.polygons, shades):
        polygon.material_index = shade
    return straw


def bake_base_color(obj, material, destination):
    """Bake only emission into existing UVs; no lighting, normal or roughness is baked."""
    scene = bpy.context.scene
    scene.render.engine = "CYCLES"
    scene.cycles.samples = 1
    scene.render.bake.margin = 8
    image = bpy.data.images.new("Pasture_BaseColor_1024", width=1024, height=1024)
    node = material.node_tree.nodes.new("ShaderNodeTexImage")
    node.image = image
    material.node_tree.nodes.active = node
    bpy.ops.object.select_all(action="DESELECT")
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    bpy.ops.object.bake(type="EMIT")
    image.filepath_raw = str(destination)
    image.file_format = "PNG"
    image.save()
    image.pack()
    return image
