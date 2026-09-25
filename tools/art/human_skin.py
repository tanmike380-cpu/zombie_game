"""Natural skin and eyes on the accepted mesh, keeping facial geometry intact."""
import math

import bpy
from mathutils import Vector


def create_material(name, color, roughness):
    material = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    material.use_nodes = True
    material.diffuse_color = (*color, 1)
    shader = next(node for node in material.node_tree.nodes if node.type == 'BSDF_PRINCIPLED')
    shader.inputs['Base Color'].default_value = (*color, 1)
    shader.inputs['Roughness'].default_value = roughness
    return material, shader


def get_region_weight(point, center, widths):
    return math.exp(-sum(((point[index]-center[index])/widths[index])**2 for index in range(3)))


def paint_skin(body):
    """Paint subtle cheeks, lips and knuckles into a reusable mesh color layer."""
    base_color = Vector((.43, .265, .165))
    attribute = body.data.color_attributes.get('HumanSkin')
    if attribute is None:
        attribute = body.data.color_attributes.new(name='HumanSkin', type='FLOAT_COLOR', domain='POINT')
    for vertex, item in zip(body.data.vertices, attribute.data):
        point = body.matrix_world @ vertex.co
        cheeks = max(get_region_weight(point, (side*.043, -.13, 1.635), (.025, .03, .022))
                     for side in (-1, 1))
        lips = get_region_weight(point, (0, -.155, 1.586), (.029, .018, .007))
        color = base_color.lerp(Vector((.45, .205, .145)), cheeks*.28)
        color = color.lerp(Vector((.27, .105, .075)), lips*.55)
        item.color = (*color, 1)
    body.data.color_attributes.active_color = attribute
    material, shader = create_material('HumanBase | warm skin', base_color, .48)
    shader.inputs['Subsurface Weight'].default_value = .075
    nodes = material.node_tree.nodes
    color_node = nodes.new('ShaderNodeVertexColor')
    color_node.layer_name = attribute.name
    material.node_tree.links.new(color_node.outputs['Color'], shader.inputs['Base Color'])
    body.data.materials.clear()
    body.data.materials.append(material)
    for face in body.data.polygons:
        face.material_index = 0


def paint_eyes():
    """Shade the existing spheres: dark irises on warm, non-white sclera."""
    eyes = [obj for obj in bpy.data.objects if '.eye.' in obj.name]
    for eye in eyes:
        material, shader = create_material('HumanBase | eyes', (.55, .50, .42), .29)
        nodes = material.node_tree.nodes
        links = material.node_tree.links
        coordinate = nodes.new('ShaderNodeTexCoord')
        separate = nodes.new('ShaderNodeSeparateXYZ')
        links.new(coordinate.outputs['Generated'], separate.inputs[0])
        # These approved eyes have local +Z facing world -Y.
        reverse = nodes.new('ShaderNodeMath')
        reverse.operation = 'SUBTRACT'
        reverse.inputs[0].default_value = 1
        links.new(separate.outputs['Z'], reverse.inputs[1])
        ramp = nodes.new('ShaderNodeValToRGB')
        entries = ramp.color_ramp.elements
        entries.remove(entries[1])
        for index, (position, color) in enumerate([
                (0, (.002, .001, .0008, 1)), (.018, (.002, .001, .0008, 1)),
                (.023, (.045, .019, .006, 1)), (.065, (.072, .036, .013, 1)),
                (.073, (.013, .008, .004, 1)), (.083, (.55, .50, .42, 1))]):
            element = entries[0] if index == 0 else entries.new(position)
            element.position, element.color = position, color
        links.new(reverse.outputs[0], ramp.inputs['Fac'])
        links.new(ramp.outputs['Color'], shader.inputs['Base Color'])
        eye.data.materials.clear()
        eye.data.materials.append(material)
    return len(eyes)
