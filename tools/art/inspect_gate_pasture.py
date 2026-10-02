"""Inspect newly imported architecture in isolated, unlit textured review scenes."""
import json
import sys
from pathlib import Path

import bpy
from mathutils import Vector

sys.path.insert(0, str(Path(__file__).parent))
from siege_preview import create_stage
from tripo_model_io import point_at

ROOT = Path(__file__).resolve().parents[2]
REVIEW = ROOT / "Builds/ArtReview/GateTowerPasture"


def inspect_architecture():
    """Render source geometry from four directions without editing imported objects."""
    report = {}
    for key, name in (("tower", "stone fortress 3d model"),
                      ("pasture", "medieval farmhouse 3d model")):
        source = bpy.data.objects[name]
        scene = bpy.data.scenes.new(f"Inspect_{key}")
        bpy.context.window.scene = scene
        obj = source.copy()
        obj.data = source.data.copy()
        scene.collection.objects.link(obj)
        obj.parent = None
        obj.matrix_world.identity()
        low = Vector(tuple(min(v.co[a] for v in obj.data.vertices) for a in range(3)))
        high = Vector(tuple(max(v.co[a] for v in obj.data.vertices) for a in range(3)))
        obj.location = Vector((-(low.x+high.x)/2, -(low.y+high.y)/2, -low.z))
        create_stage(scene, "GreekFire")
        scene.camera.data.ortho_scale = 1.7
        report[key] = {"source": name, "bounds": [list(low), list(high)],
                       "vertices": len(obj.data.vertices),
                       "materials": [mat.name for mat in obj.data.materials]}
        for view, location in enumerate(((1.5,-2,1.5), (-1.5,2,1.5), (0,-2,.65), (0,2,.65))):
            scene.camera.location = location
            point_at(scene.camera, (0,0,(high.z-low.z)/2))
            scene.render.filepath = str(REVIEW / f"{key}-source-{view}.png")
            bpy.ops.render.render(write_still=True)
        if key == "pasture":
            # Temporary geometry diagnostic only: no light objects and no source save.
            scene.display.shading.color_type = "SINGLE"
            scene.display.shading.light = "STUDIO"
            scene.display.shading.show_cavity = True
            scene.camera.location = (1.5, -2, 1.5)
            point_at(scene.camera, (0,0,.15))
            scene.render.filepath = str(REVIEW / "pasture-shape.png")
            bpy.ops.render.render(write_still=True)
    (REVIEW / "source-audit.json").write_text(json.dumps(report, indent=2))
    print(json.dumps(report), flush=True)


if __name__ == "__main__":
    inspect_architecture()
