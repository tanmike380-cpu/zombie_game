"""Import/audit/render helpers for user-owned Tripo FBX files, preserving source rigs."""
import json
from pathlib import Path
import bpy
from mathutils import Vector


def import_model(folder):
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete(use_global=False)
    bpy.ops.import_scene.fbx(filepath=str(next(folder.glob("*.fbx")).resolve()))
    rig = next(obj for obj in bpy.context.scene.objects if obj.type == "ARMATURE")
    mesh = next(obj for obj in bpy.context.scene.objects if obj.type == "MESH")
    for image in bpy.data.images:
        if image.source != "FILE":
            continue
        path = next(folder.rglob(Path(image.filepath.replace("\\", "/")).name), None)
        if path:
            image.filepath = str(path.resolve())
            image.reload()
            image.pack()
    return rig, mesh


def audit_model(rig, mesh, path):
    neighbors = [set() for _ in mesh.data.vertices]
    for edge in mesh.data.edges:
        a, b = edge.vertices
        neighbors[a].add(b)
        neighbors[b].add(a)
    remaining = set(range(len(neighbors)))
    components = []
    while remaining:
        queue, indices = [remaining.pop()], []
        while queue:
            current = queue.pop()
            indices.append(current)
            found = neighbors[current] & remaining
            remaining.difference_update(found)
            queue.extend(found)
        points = [mesh.data.vertices[i].co for i in indices]
        components.append({"count": len(indices), "indices": indices,
                           "min": [min(v[i] for v in points) for i in range(3)],
                           "max": [max(v[i] for v in points) for i in range(3)]})
    report = {"bones": [{"name": b.name, "head": list(b.head_local), "tail": list(b.tail_local)} for b in rig.data.bones],
              "actions": [a.name for a in bpy.data.actions],
              "components": sorted(components, key=lambda c: -c["count"]),
              "triangles": sum(len(p.vertices) - 2 for p in mesh.data.polygons)}
    path.write_text(json.dumps(report, indent=2))
    return report


def point_at(obj, target):
    obj.rotation_euler = (Vector(target) - obj.location).to_track_quat("-Z", "Y").to_euler()


def setup_render():
    scene = bpy.context.scene
    scene.render.engine = "CYCLES"
    scene.cycles.samples = 24
    scene.cycles.use_denoising = True
    scene.render.resolution_x, scene.render.resolution_y = 900, 1000
    scene.render.resolution_percentage = 100
    scene.world.color = (.25, .25, .25)
    scene.view_settings.view_transform = "AgX"
    for location, energy, size in [((2, -3, 4), 350, 3), ((-2, -1, 2), 180, 2), ((0, 2, 3), 280, 2)]:
        bpy.ops.object.light_add(type="AREA", location=location)
        bpy.context.object.data.energy = energy
        bpy.context.object.data.shape = "DISK"
        bpy.context.object.data.size = size
        point_at(bpy.context.object, (0, 0, .5))
    bpy.ops.object.camera_add(location=(1.4, -2.3, 1.4))
    camera = bpy.context.object
    camera.data.type = "ORTHO"
    camera.data.ortho_scale = 1.35
    point_at(camera, (0, 0, .49))
    scene.camera = camera
