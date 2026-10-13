"""Pin two authorized CC0 native review packages; inspect without source edits."""

import argparse
import collections
import json
import logging
from pathlib import Path
import sys

sys.path.insert(0, str(Path(__file__).resolve().parent))
from download_showcase_vegetation import download_file, get_json, PROJECT_ROOT, SOURCE_ROOT


ASSET_RESOLUTIONS = {"island_tree_01": "1k", "boulder_01": "2k"}
MANIFEST_PATH = PROJECT_ROOT / "art/environment/2026-10-14-free-source-review.json"


def load_manifest():
    """Read only this review manifest, or return the new review envelope."""
    if MANIFEST_PATH.exists():
        return json.loads(MANIFEST_PATH.read_text(encoding="utf-8"))
    return {"schema_version": 1, "review_date": "2026-10-14", "assets": [],
            "scope": "Native CC0 candidates only; no Blender category mutation or geometry edits",
            "status": "native_download_and_inspection_pending_visual_review",
            "stone_identity": "Licensed natural stone deposit candidate; NOT user Tripo stone mine",
            "big_oak_status": "Not acquired; account-library addition incomplete"}


def save_manifest(manifest):
    """Write the assigned review artifact, never existing production handoffs."""
    MANIFEST_PATH.write_text(json.dumps(manifest, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")


def validate_relative_path(relative_path):
    """Reject traversal and absolute API include paths before download."""
    path = Path(relative_path)
    if path.is_absolute() or ".." in path.parts:
        raise ValueError(f"Unsafe official include path: {relative_path}")


def build_download_records(asset_id, resolution, files):
    """Select the native FBX, its complete includes, and original PNG/JPG PBR."""
    model = files["fbx"][resolution]["fbx"]
    records = {f"{asset_id}_{resolution}.fbx": model, **model.get("include", {})}
    channels = {}
    for channel_name, resolutions in files.items():
        if channel_name in ("blend", "fbx", "gltf", "usd") or resolution not in resolutions:
            continue
        formats = resolutions[resolution]
        entry = formats.get("png") or formats.get("jpg")
        if not entry or "url" not in entry:
            continue
        relative_path = "textures/" + entry["url"].split("/")[-1]
        records[relative_path] = entry
        channels[channel_name] = {"path": str((SOURCE_ROOT / asset_id / relative_path).relative_to(PROJECT_ROOT)),
                                  "colorspace": "sRGB" if "diff" in channel_name.lower() else "linear",
                                  "component": "rgb" if "diff" in channel_name.lower() or "nor_" in channel_name else "red"}
    return records, channels


def download_review_asset(asset_id):
    """Verify original bytes against official MD5 and record local SHA256."""
    resolution = ASSET_RESOLUTIONS[asset_id]
    metadata = get_json(f"https://api.polyhaven.com/info/{asset_id}")
    files = get_json(f"https://api.polyhaven.com/files/{asset_id}")
    records, channels = build_download_records(asset_id, resolution, files)
    downloaded = []
    for relative_path, record in records.items():
        validate_relative_path(relative_path)
        downloaded.append(download_file(asset_id, relative_path, record))
    attach_channel_checksums(channels, downloaded)
    logging.info("Verified native %s (%d files)", asset_id, len(downloaded))
    return {"id": asset_id, "title": metadata["name"], "authors": metadata.get("authors", {}),
            "source_url": f"https://polyhaven.com/a/{asset_id}", "license": "CC0-1.0",
            "license_url": "https://polyhaven.com/license", "official_info": metadata,
            "official_info_api": f"https://api.polyhaven.com/info/{asset_id}",
            "official_files_api": f"https://api.polyhaven.com/files/{asset_id}",
            "resolution": resolution, "files": downloaded, "texture_channels": channels,
            "model_path": downloaded[0]["path"], "model_sha256": downloaded[0]["sha256"],
            "geometry_policy": "Original source unchanged; no decimation, refit, UV or normal editing",
            "review_status": "download_verified_native_inspection_pending"}


def attach_channel_checksums(channels, downloaded):
    """Bind each original texture channel to its already verified byte record."""
    by_path = {record["path"]: record for record in downloaded}
    for name, channel in channels.items():
        channel["sha256"] = by_path[channel["path"]]["sha256"]
        channel["component"] = "rgb" if "diff" in name.lower() or "nor_" in name else "red"


def build_material_channels(asset, material_name, prefix=""):
    """Map named native slots to official channels without synthesizing maps."""
    names = {"base_color": prefix + "diff" if prefix else "Diffuse",
             "normal_gl": prefix + "nor_gl", "roughness": prefix + "rough" if prefix else "Rough",
             "AO": prefix + "ao" if prefix else "AO", "alpha": prefix + "alpha"}
    return {"name": material_name, "metallic": 0,
            "alpha_mode": "cutout" if prefix == "leaves_" else "opaque",
            "double_sided": prefix == "leaves_",
            "roughness_to_smoothness": "Convert once: smoothness = 1 - source roughness",
            "channels": {name: asset["texture_channels"][key] for name, key in names.items()
                         if key in asset["texture_channels"]}}


def finalize_review_manifest():
    """Record native budget decisions, not unperformed visual approval."""
    manifest = load_manifest()
    for asset in manifest["assets"]:
        attach_channel_checksums(asset["texture_channels"], asset["files"])
        asset["material_channel_groups"] = [build_material_channels(asset, asset["id"])]
        if asset["id"] == "island_tree_01":
            asset["material_channel_groups"].extend([
                build_material_channels(asset, "island_tree_01_leaves", "leaves_"),
                build_material_channels(asset, "island_tree_01_branches", "branches_")])
            lods = [mesh for mesh in asset["native_blend_inspection"]["meshes"]
                    if mesh["name"] in ("island_tree_01_LOD0", "island_tree_01_LOD1")]
            asset["native_full_tree_lods"] = [{"name": mesh["name"], "triangles": mesh["triangles"]} for mesh in lods]
            asset["review_status"] = "rejected_for_dense_rts_forest_hero_only_candidate"
            asset["selection"] = {"dense_forest_approved": False, "selected_forest_lods": [],
                                  "reason": "FBX only has 1,599,403-triangle LOD0. Blend full-tree LOD1 is 519,809 triangles; helper branch/leaf LODs and Geometry Nodes base are not complete trees.",
                                  "hero_preview": "Optional single original FBX tree only if parent explicitly accepts cost; not a forest replacement"}
        else:
            asset["review_status"] = "native_lod_candidate_ready_for_unity_visual_review"
            asset["selection"] = {"near_native_object": "boulder_01_LOD2", "far_native_object": "boulder_01_LOD3",
                                  "near_triangles": 16530, "far_triangles": 8264,
                                  "note": "Original author LODs only. Disable other overlapping native meshes; source bounds/pivot retained; ground whole root from measured bounds, uniform scale only.",
                                  "identity": "Licensed natural stone deposit candidate, not user's Tripo stone mine; no mining/economy behavior"}
        asset["unit_note"] = "Native FBX imported with scale 1 into Blender; readback world bounds retained. Unity measures its own whole-root bounds; no per-axis or per-piece refitting."
    manifest["status"] = "native_verification_complete_boulder_visual_review_pending_tree_rejected_for_forest"
    save_manifest(manifest)


def download_review_sources():
    """Restore pinned candidates or download only the two assigned new IDs."""
    manifest = load_manifest()
    for asset_id in ASSET_RESOLUTIONS:
        existing = next((asset for asset in manifest["assets"] if asset["id"] == asset_id), None)
        if existing:
            for record in existing["files"]:
                relative_path = Path(record["path"]).relative_to(SOURCE_ROOT.relative_to(PROJECT_ROOT) / asset_id)
                download_file(asset_id, str(relative_path), record)
        else:
            manifest["assets"].append(download_review_asset(asset_id))
            save_manifest(manifest)


def download_native_blend(asset_id):
    """Add official native Blend only when the FBX omitted advertised LODs."""
    manifest = load_manifest()
    asset = next(asset for asset in manifest["assets"] if asset["id"] == asset_id)
    resolution = ASSET_RESOLUTIONS[asset_id]
    record = get_json(f"https://api.polyhaven.com/files/{asset_id}")["blend"][resolution]["blend"]
    result = download_file(asset_id, f"{asset_id}_{resolution}.blend", record)
    if not any(file["path"] == result["path"] for file in asset["files"]):
        asset["files"].append(result)
    asset["native_blend_path"] = result["path"]
    asset["native_blend_sha256"] = result["sha256"]
    save_manifest(manifest)


def inspect_mesh_object(obj):
    """Read native imported FBX topology, world bounds, UV and authored transform."""
    from mathutils import Vector
    mesh = obj.data
    mesh.calc_loop_triangles()
    corners = [obj.matrix_world @ Vector(point) for point in obj.bound_box]
    minimum = [min(point[axis] for point in corners) for axis in range(3)]
    maximum = [max(point[axis] for point in corners) for axis in range(3)]
    material_counts = collections.Counter(triangle.material_index for triangle in mesh.loop_triangles)
    return {"name": obj.name, "vertices": len(mesh.vertices), "triangles": len(mesh.loop_triangles),
            "materials": [material.name if material else None for material in mesh.materials],
            "triangles_by_material": {str(index): count for index, count in material_counts.items()},
            "bounds_blender_min": minimum, "bounds_blender_max": maximum,
            "dimensions_blender_xyz_m": [maximum[index] - minimum[index] for index in range(3)],
            "uv_layers": list(mesh.uv_layers.keys()),
            "active_uv": mesh.uv_layers.active.name if mesh.uv_layers.active else None,
            "has_custom_normals": mesh.has_custom_normals,
            "parent": obj.parent.name if obj.parent else None,
            "matrix_world": [list(row) for row in obj.matrix_world],
            "modifiers": [modifier.type for modifier in obj.modifiers]}


def inspect_native_blend(asset_id):
    """Read exact downloaded Blend in background; do not save, mutate or export."""
    import bpy
    manifest = load_manifest()
    asset = next(asset for asset in manifest["assets"] if asset["id"] == asset_id)
    expected_path = (PROJECT_ROOT / asset["native_blend_path"]).resolve()
    if Path(bpy.data.filepath).resolve() != expected_path:
        raise RuntimeError(f"Only downloaded candidate Blend may be inspected: {expected_path}")
    meshes = [inspect_mesh_object(obj) for obj in bpy.data.objects if obj.type == "MESH"]
    asset["native_blend_inspection"] = {"meshes": meshes,
                                        "native_lod_names": [mesh["name"] for mesh in meshes if "lod" in mesh["name"].lower()],
                                        "collections": list(bpy.data.collections.keys()),
                                        "geometry_edited": False}
    save_manifest(manifest)
    print("NATIVE_BLEND_LODS", json.dumps([mesh for mesh in meshes if "lod" in mesh["name"].lower()], ensure_ascii=False))


def inspect_native_fbx(asset_id):
    """Import only in factory-background memory and record facts; never save blend."""
    import bpy
    manifest = load_manifest()
    asset = next(asset for asset in manifest["assets"] if asset["id"] == asset_id)
    if bpy.data.filepath:
        raise RuntimeError("Inspection requires factory-startup, not any user/category scene")
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete(use_global=False)
    bpy.ops.import_scene.fbx(filepath=str(PROJECT_ROOT / asset["model_path"]), use_custom_normals=True)
    bpy.context.view_layer.update()
    meshes = [inspect_mesh_object(obj) for obj in bpy.data.objects if obj.type == "MESH"]
    materials = []
    for material in bpy.data.materials:
        if not material.use_nodes:
            continue
        materials.append({"name": material.name,
                          "backface_culling": material.use_backface_culling,
                          "images": [{"node": node.name, "image": node.image.name,
                                      "filepath": node.image.filepath,
                                      "resolution": list(node.image.size),
                                      "colorspace": node.image.colorspace_settings.name}
                                     for node in material.node_tree.nodes
                                     if node.type == "TEX_IMAGE" and node.image]})
    asset["native_inspection"] = {"blender_version": bpy.app.version_string, "meshes": meshes,
                                  "materials_from_fbx": materials, "scene_unit_scale": bpy.context.scene.unit_settings.scale_length,
                                  "note": "FBX import material approximation, not full original Blender leaf-translucency graph",
                                  "native_lod_names": [mesh["name"] for mesh in meshes if "lod" in mesh["name"].lower()]}
    asset["review_status"] = "native_inspected_visual_review_pending"
    save_manifest(manifest)
    print("NATIVE_REVIEW", json.dumps({"id": asset_id, "meshes": meshes}, ensure_ascii=False))


def main():
    """Dispatch download in Python or inspection in isolated background Blender."""
    logging.basicConfig(level=logging.INFO, format="%(levelname)s %(message)s")
    arguments = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else sys.argv[1:]
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("mode", choices=("download", "inspect", "download-blend", "inspect-blend", "finalize"))
    parser.add_argument("asset_id", nargs="?", choices=tuple(ASSET_RESOLUTIONS))
    options = parser.parse_args(arguments)
    if options.mode == "download":
        download_review_sources()
    elif options.mode == "finalize":
        finalize_review_manifest()
    elif options.mode == "download-blend" and options.asset_id:
        download_native_blend(options.asset_id)
    elif options.mode == "inspect-blend" and options.asset_id:
        inspect_native_blend(options.asset_id)
    elif options.asset_id:
        inspect_native_fbx(options.asset_id)
    else:
        parser.error("inspect requires an authorized asset_id")


if __name__ == "__main__":
    main()
