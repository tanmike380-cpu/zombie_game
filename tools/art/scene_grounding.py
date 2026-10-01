"""Ground imported mesh hierarchies without separating their armatures or props."""
import bpy


def collect_model_roots():
    """Return imported roots, excluding Blender's default cube and presentation objects."""
    return [obj for obj in bpy.context.scene.objects
            if obj.parent is None and obj.type in {"MESH", "ARMATURE", "EMPTY"}
            and obj.name != "Cube" and not obj.get("review_fixture")]


def collect_meshes(root):
    """Return all mesh objects belonging to one root hierarchy."""
    return [obj for obj in [root, *root.children_recursive] if obj.type == "MESH"]


def measure_bounds(root):
    """Measure evaluated world-space vertices, including current skin deformation."""
    dependency_graph = bpy.context.evaluated_depsgraph_get()
    points = []
    for mesh in collect_meshes(root):
        evaluated = mesh.evaluated_get(dependency_graph)
        points.extend(evaluated.matrix_world @ vertex.co for vertex in evaluated.data.vertices)
    if not points:
        raise ValueError(f"No mesh vertices under model root: {root.name}")
    return ([min(point[axis] for point in points) for axis in range(3)],
            [max(point[axis] for point in points) for axis in range(3)])


def ground_models(roots):
    """Shift only root Z; preserve XY, rest bones, weights, scale and hierarchy."""
    report = []
    for root in roots:
        before = root.matrix_world.copy()
        low, _ = measure_bounds(root)
        grounded = before.copy()
        grounded.translation.z -= low[2]
        root.matrix_world = grounded
        bpy.context.view_layer.update()
        after_low, after_high = measure_bounds(root)
        if abs(after_low[2]) > 0.00001:
            raise RuntimeError(f"Grounding failed for {root.name}: {after_low[2]}")
        if (root.matrix_world.translation.xy - before.translation.xy).length > 0.00001:
            raise RuntimeError(f"Grounding changed XY for {root.name}")
        report.append({"name": root.name, "z_shift": -low[2],
                       "min": after_low, "max": after_high})
    return report
