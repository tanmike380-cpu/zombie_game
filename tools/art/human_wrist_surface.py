"""Bridge the simplified hands to the baked forearms with real shared topology."""
import math

import bmesh
import bpy
from mathutils import Vector

from freeze_human_base import BODY_NAME
from human_rts_hands import HAND_PREFIX


def collect_boundary_loops(mesh):
    remaining = {edge for edge in mesh.edges if edge.is_boundary}
    loops = []
    while remaining:
        seed = remaining.pop()
        edges, pending = {seed}, list(seed.verts)
        while pending:
            vertex = pending.pop()
            for edge in vertex.link_edges:
                if edge in remaining:
                    remaining.remove(edge)
                    edges.add(edge)
                    pending.extend(edge.verts)
        vertices = list({vertex for edge in edges for vertex in edge.verts})
        center = sum((vertex.co for vertex in vertices), Vector())/len(vertices)
        loops.append((vertices, center))
    return loops


def find_wrist_loop(mesh, wrist):
    loops = collect_boundary_loops(mesh)
    if not loops:
        raise ValueError('No forearm boundary available for wrist bridge')
    loop, center = min(loops, key=lambda item: (item[1]-wrist).length)
    if (center-wrist).length > .08:
        raise ValueError(f'Wrist boundary too far from wrist: {(center-wrist).length:.3f}m')
    return loop, center


def bridge_rings(mesh, first, second, axis):
    """Connect unequal ring counts without leaving a seam or intersecting caps."""
    center = sum((vertex.co for vertex in first), Vector())/len(first)
    across = axis.cross(Vector((0, 0, 1)))
    if across.length < .01:
        across = axis.cross(Vector((1, 0, 0)))
    across.normalize()
    normal = axis.cross(across)
    def order_ring(vertices):
        allowed = set(vertices)
        local_center = sum((vertex.co for vertex in vertices), Vector())/len(vertices)
        def local_angle(vertex):
            delta = vertex.co-local_center
            return math.atan2(delta.dot(normal), delta.dot(across))
        start = min(vertices, key=local_angle)
        ordered = [start]
        previous, current = None, start
        while True:
            choices = [edge.other_vert(current) for edge in current.link_edges
                       if edge.is_boundary and edge.other_vert(current) in allowed
                       and edge.other_vert(current) != previous]
            if not choices:
                raise ValueError('Wrist boundary is not a closed ring')
            following = choices[0]
            if following == start:
                break
            if following in ordered:
                raise ValueError('Wrist ring branches into itself')
            ordered.append(following)
            previous, current = current, following
        if len(ordered) != len(vertices):
            raise ValueError('Wrist boundary contains multiple rings')
        area = sum(((a.co-center).cross(b.co-center) for a, b in
                    zip(ordered, ordered[1:]+ordered[:1])), Vector()).dot(axis)
        return ordered if area > 0 else ordered[:1]+list(reversed(ordered[1:]))
    first, second = order_ring(first), order_ring(second)
    def angular_steps(ring):
        local_center = sum((vertex.co for vertex in ring), Vector())/len(ring)
        phases = [math.atan2((vertex.co-local_center).dot(normal), (vertex.co-local_center).dot(across))
                  for vertex in ring]
        return [(phase-phases[0]) % math.tau/math.tau for phase in phases]+[1.0]
    first_steps, second_steps = angular_steps(first), angular_steps(second)
    a_index = b_index = 0
    created = []
    while a_index < len(first) or b_index < len(second):
        a_next = first_steps[min(a_index+1, len(first))] if a_index < len(first) else 2
        b_next = second_steps[min(b_index+1, len(second))] if b_index < len(second) else 2
        a, b = first[a_index % len(first)], second[b_index % len(second)]
        if abs(a_next-b_next) < 1e-8:
            vertices = (a, first[(a_index+1) % len(first)], second[(b_index+1) % len(second)], b)
            a_index += 1
            b_index += 1
        elif a_next < b_next:
            vertices = (a, first[(a_index+1) % len(first)], b)
            a_index += 1
        else:
            vertices = (a, second[(b_index+1) % len(second)], b)
            b_index += 1
        face = mesh.faces.new(vertices)
        face.smooth = True
        created.append(face)
    bmesh.ops.recalc_face_normals(mesh, faces=list(mesh.faces))
    if any(edge.is_boundary for face in created for edge in face.edges):
        raise ValueError('Wrist bridge still has an open boundary')
    return len(created)


def attach_hand(body, hand, wrist, axis):
    body_mesh = bmesh.new()
    body_mesh.from_mesh(body.data)
    old_ring, _ = find_wrist_loop(body_mesh, wrist)
    cut_plane = wrist-axis*.105
    # Follow only the connected distal forearm patch. A global plane cut would
    # also cut the torso/other arm, which must remain untouched.
    patch_faces = set()
    pending = [face for vertex in old_ring for face in vertex.link_faces]
    while pending:
        face = pending.pop()
        if face in patch_faces or all((vertex.co-cut_plane).dot(axis) < -.025 for vertex in face.verts):
            continue
        patch_faces.add(face)
        pending.extend(other for edge in face.edges for other in edge.link_faces if other not in patch_faces)
    patch_edges = {edge for face in patch_faces for edge in face.edges}
    patch_vertices = {vertex for face in patch_faces for vertex in face.verts}
    bmesh.ops.bisect_plane(body_mesh, geom=list(patch_vertices)+list(patch_edges)+list(patch_faces),
                          dist=.000001, plane_co=cut_plane, plane_no=axis)
    distal, pending = set(), list(old_ring)
    while pending:
        vertex = pending.pop()
        if vertex in distal or (vertex.co-cut_plane).dot(axis) <= .00001:
            continue
        distal.add(vertex)
        pending.extend(edge.other_vert(vertex) for edge in vertex.link_edges)
    bmesh.ops.delete(body_mesh, geom=list(distal), context='VERTS')
    _, body_center = find_wrist_loop(body_mesh, cut_plane)
    body_mesh.to_mesh(body.data)
    body_mesh.free()
    hand_mesh = bmesh.new()
    hand_mesh.from_mesh(hand.data)
    plane = wrist-axis*.014
    bmesh.ops.bisect_plane(hand_mesh, geom=list(hand_mesh.verts)+list(hand_mesh.edges)+list(hand_mesh.faces),
                          dist=.000001, plane_co=plane, plane_no=axis, clear_inner=True)
    _, hand_center = find_wrist_loop(hand_mesh, plane)
    hand_mesh.to_mesh(hand.data)
    hand_mesh.free()
    bpy.ops.object.select_all(action='DESELECT')
    body.select_set(True)
    hand.select_set(True)
    bpy.context.view_layer.objects.active = body
    bpy.ops.object.join()
    mesh = bmesh.new()
    mesh.from_mesh(body.data)
    first, _ = find_wrist_loop(mesh, body_center)
    second, _ = find_wrist_loop(mesh, hand_center)
    if set(first) == set(second):
        raise ValueError('Could not distinguish hand and forearm boundaries')
    count = bridge_rings(mesh, first, second, axis)
    mesh.to_mesh(body.data)
    mesh.free()
    body.data.update()
    return {'center': list(body_center), 'bridge_faces': count, 'open_bridge_edges': 0}


def join_wrist_surfaces(parts, scale):
    body = next(obj for obj in parts if obj.name.startswith(BODY_NAME))
    rig = bpy.data.objects['Equipment | editable holding pose rig']
    report = {}
    for side, role in ((-1, 'trigger'), (1, 'support')):
        hand = next(obj for obj in parts if obj.name.startswith(HAND_PREFIX+role))
        bone = rig.pose.bones[f'forearm.{side}']
        wrist = Vector(tuple(bone.tail[index]*scale[index] for index in range(3)))
        elbow = Vector(tuple(bone.head[index]*scale[index] for index in range(3)))
        # Drop the reference before Blender destroys the joined hand object.
        parts.remove(hand)
        report[role] = attach_hand(body, hand, wrist, (wrist-elbow).normalized())
    return report


def verify_wrist_surfaces(objects, report):
    """Check saved geometry near both seams, not just the report's success flag."""
    if set(report) != {'trigger', 'support'}:
        raise ValueError('Both continuous wrist surfaces must be present')
    for role, entry in report.items():
        if entry['bridge_faces'] < 8 or entry['open_bridge_edges'] != 0:
            raise ValueError(f'Incomplete wrist bridge: {role}')
        center = Vector(entry['center'])
        count = 0
        for obj in objects:
            if not any(material and material.name == 'HumanBase | warm skin' for material in obj.data.materials):
                continue
            mesh = bmesh.new()
            mesh.from_mesh(obj.data)
            count += sum((vertex.co-center).length < .045 for vertex in mesh.verts)
            if any(edge.is_boundary and ((edge.verts[0].co+edge.verts[1].co)*.5-center).length < .045
                   for edge in mesh.edges):
                mesh.free()
                raise ValueError(f'Open skin edge remains at {role} wrist')
            mesh.free()
        if count < 8:
            raise ValueError(f'Missing skin around the {role} wrist')
