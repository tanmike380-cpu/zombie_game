"""Pack four approved original grass images without changing authored scene data.

Run only in a background Blender process. The live, unsaved Zombie document is
not addressed. A recovery copy, candidate reopen and final reopen are mandatory.
"""
import copy
import hashlib
import json
import shutil
import subprocess
import sys
from datetime import datetime, timezone
from pathlib import Path

import bpy
import numpy as np

sys.path.insert(0, str(Path(__file__).parent))
from fortified_city_assembly import capture_protected_state, hash_payload, read_nodes, read_rna_values

ROOT = Path(__file__).resolve().parents[2]
MASTER = ROOT / "art/Blender主文件/ZombieGame_Environment.blend"
EXPECTED_SHA = "cf8310ae2f8d643653fd8cf9e96ef7dea0570f586a76e2d0f62911c7d3803301"
HANDOFF = ROOT / "art/environment/portable_handoff.json"
SOURCE_FOLDER = ROOT / "Builds/EnvironmentCandidateDownloads/blender-20261015-vE1sPP/grass_medium_02/textures"
IMAGE_FILES = {
    "grass_medium_02_alpha.png": "grass_medium_02_alpha_1k.png",
    "grass_medium_02_diff.png": "grass_medium_02_diff_1k.jpg",
    "grass_medium_02_nor_gl.png": "grass_medium_02_nor_gl_1k.exr",
    "grass_medium_02_rough.png": "grass_medium_02_rough_1k.exr",
}
ORPHAN_MATERIALS = ("M_Quercus", "M_Quercus_LOD1")


def hash_file(path):
    """Return the exact saved file SHA, not an image conversion hash."""
    return hashlib.sha256(path.read_bytes()).hexdigest()


def read_pixel_sha(image):
    """Hash all decoded float32 image pixels in their existing Blender colorspace."""
    pixels = np.empty(len(image.pixels), dtype=np.float32)
    image.pixels.foreach_get(pixels)
    return hashlib.sha256(pixels.tobytes()).hexdigest()


def capture_source_state():
    """Extend the shared guard with raw image paths for location-independent reopening."""
    state = capture_protected_state()
    for image in bpy.data.images:
        state["images"][image.name]["filepath"] = image.filepath
    state["data_names"] = {
        category: sorted(getattr(bpy.data, category).keys())
        for category in ("objects", "meshes", "materials", "images", "actions", "collections",
                         "lights", "cameras", "worlds", "armatures", "scenes", "libraries")
    }
    return state


def verify_source_state(expected):
    """Reject any change beyond the four whitelisted packed byte buffers."""
    actual = capture_source_state()
    for category, records in expected.items():
        if actual[category] != records:
            changed = [name for name in records if actual[category].get(name) != records[name]]
            raise AssertionError(f"Portable packing changed protected {category}: {changed}")
    return {category: len(records) for category, records in expected.items() if category != "data_names"}


def require_authoring_source():
    """Require the exact audited disk Environment, not a live document or stale file."""
    if not bpy.app.background or Path(bpy.data.filepath).resolve() != MASTER:
        raise RuntimeError("Packing requires background Blender on the formal Environment source")
    if hash_file(MASTER) != EXPECTED_SHA or len(bpy.data.objects) != 91:
        raise RuntimeError("Environment changed since audit; refuse to overwrite and re-audit first")
    if HANDOFF.exists():
        raise RuntimeError("Portable handoff already exists; refuse duplicate source mutation")


def record_original_images():
    """Record the exact four existing source files and their decoded pixels."""
    records = []
    for name, filename in IMAGE_FILES.items():
        image = bpy.data.images[name]
        source = Path(bpy.path.abspath(image.filepath)).resolve()
        if source != SOURCE_FOLDER / filename or not source.is_file() or image.packed_file:
            raise AssertionError(f"Expected existing unpacked original image not found: {name}")
        records.append({"image_name": name, "original_file": str(source.relative_to(ROOT)),
                        "original_file_bytes": source.stat().st_size,
                        "original_file_sha256": hash_file(source),
                        "decoded_float32_pixels_sha256": read_pixel_sha(image),
                        "size": list(image.size), "colorspace": image.colorspace_settings.name,
                        "source_format": image.file_format})
    return records


def pack_original_images(records):
    """Embed original unchanged JPG/EXR/PNG bytes, never replace or resave images."""
    for record in records:
        image = bpy.data.images[record["image_name"]]
        image.pack()
        verify_packed_image(record)


def retain_orphan_materials(expected):
    """Keep two existing zero-user materials with the specifically authorized storage flag."""
    changes = []
    for name in ORPHAN_MATERIALS:
        material = bpy.data.materials[name]
        before_settings, before_nodes = read_rna_values(material), read_nodes(material)
        if material.users != 0 or material.use_fake_user:
            raise AssertionError(f"Authorized orphan material baseline changed: {name}")
        material.use_fake_user = True
        expected_settings = dict(before_settings, use_fake_user=True)
        if read_rna_values(material) != expected_settings or read_nodes(material) != before_nodes:
            raise AssertionError(f"Retaining orphan material altered more than its storage flag: {name}")
        expected["materials"][name] = hash_payload([expected_settings, before_nodes])
        changes.append({"material": name, "property": "use_fake_user", "before": False, "after": True,
                        "reason": "Preserve the original unused material; ordinary Blender save would otherwise discard it.",
                        "nodes_links_inputs_unchanged": True})
    return changes


def verify_packed_image(record):
    """Check encoded source bytes and decoded numerical pixels after each reopen."""
    image = bpy.data.images[record["image_name"]]
    if not image.packed_file:
        raise AssertionError(f"Original image is not packed: {image.name}")
    encoded_sha = hashlib.sha256(image.packed_file.data).hexdigest()
    if encoded_sha != record["original_file_sha256"] or read_pixel_sha(image) != record["decoded_float32_pixels_sha256"]:
        raise AssertionError(f"Original image bytes or decoded pixels changed: {image.name}")
    if image.colorspace_settings.name != record["colorspace"] or list(image.size) != record["size"]:
        raise AssertionError(f"Image resolution or colorspace changed: {image.name}")


def inspect_portable_dependencies():
    """Require every used external datablock to be packed or tracked and relative."""
    checked, errors = [], []
    groups = ("images", "libraries", "fonts", "sounds", "movieclips", "cache_files", "volumes")
    for category in groups:
        for item in getattr(bpy.data, category):
            raw = getattr(item, "filepath", "")
            if item.users == 0 or not raw or raw == "<builtin>":
                continue
            if getattr(item, "packed_file", None) or getattr(item, "packed_files", []):
                checked.append({"category": category, "name": item.name, "packed": True})
                continue
            path = Path(bpy.path.abspath(raw, library=getattr(item, "library", None))).resolve()
            inside = path.is_relative_to(ROOT)
            tracked = inside and subprocess.run(
                ["git", "ls-files", "--error-unmatch", "--", str(path.relative_to(ROOT))],
                cwd=ROOT, stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL).returncode == 0
            if not (raw.startswith("//") and path.exists() and tracked):
                errors.append({"category": category, "name": item.name, "filepath": raw})
            else:
                checked.append({"category": category, "name": item.name, "project_relative": str(path.relative_to(ROOT))})
    if errors:
        raise AssertionError(f"Environment still has non-portable used dependencies: {errors}")
    return {"passed": True, "used_dependencies_checked": len(checked), "nonportable": [],
            "packed_image_count": sum(bool(image.packed_file) for image in bpy.data.images),
            "image_datablocks": len(bpy.data.images), "linked_libraries": len(bpy.data.libraries)}


def create_recovery_snapshot():
    """Keep the original source in a fresh recovery directory before saving anything."""
    timestamp = datetime.now().strftime("%Y%m%d-%H%M%S")
    folder = ROOT / "Builds/Recovery" / ("EnvironmentPortable-" + timestamp)
    folder.mkdir(parents=True, exist_ok=False)
    backup = folder / "ZombieGame_Environment-before-pack.blend"
    shutil.copy2(MASTER, backup)
    if hash_file(backup) != EXPECTED_SHA:
        raise AssertionError("Environment recovery snapshot differs from audited source")
    return folder, backup


def reopen_and_verify(path, expected, records):
    """Independently read a saved candidate or final file and recheck scene and pixels."""
    bpy.ops.wm.open_mainfile(filepath=str(path), load_ui=False)
    counts = verify_source_state(expected)
    for record in records:
        verify_packed_image(record)
    return {"passed": True, "protected_counts": counts, "original_encoded_bytes_unchanged": True,
            "decoded_float32_pixels_unchanged": True, "dependencies": inspect_portable_dependencies()}


def save_portable_source(folder, expected, records):
    """Verify a saved candidate before touching the official path; then recheck final."""
    candidate = folder / "ZombieGame_Environment-packed-candidate.blend"
    bpy.context.preferences.filepaths.save_version = 0
    bpy.ops.wm.save_as_mainfile(filepath=str(candidate), copy=True, relative_remap=False, check_existing=False)
    candidate_proof = reopen_and_verify(candidate, expected, records)
    if hash_file(MASTER) != EXPECTED_SHA:
        raise RuntimeError("Original Environment changed during candidate validation; refuse final save")
    bpy.ops.wm.save_as_mainfile(filepath=str(MASTER), relative_remap=False, check_existing=False)
    final_sha = hash_file(MASTER)
    final_proof = reopen_and_verify(MASTER, expected, records)
    if hash_file(MASTER) != final_sha:
        raise AssertionError("Final Environment changed during independent verification")
    return final_sha, candidate_proof, final_proof


def run_portable_packing():
    """Apply the four-image storage-only repair and produce a traceable handoff."""
    require_authoring_source()
    folder, backup = create_recovery_snapshot()
    before_state = capture_source_state()
    records = record_original_images()
    expected = copy.deepcopy(before_state)
    for record in records:
        expected["images"][record["image_name"]]["packed"] = record["original_file_sha256"]
    storage_flag_changes = retain_orphan_materials(expected)
    pack_original_images(records)
    verify_source_state(expected)
    final_sha, candidate_proof, final_proof = save_portable_source(folder, expected, records)
    evidence = {"before": before_state, "expected_after": expected,
                "candidate_reopen": candidate_proof, "final_reopen": final_proof}
    evidence_path = ROOT / "art/environment/portable_verification.json"
    evidence_path.write_text(json.dumps(evidence, ensure_ascii=False, indent=2))
    handoff = {"schema_version": 1, "status": "portable source storage repair verified",
               "verified_at_utc": datetime.now(timezone.utc).isoformat(),
               "master_file": str(MASTER.relative_to(ROOT)), "source_before_sha256": EXPECTED_SHA,
               "master_sha256": final_sha, "file_bytes": MASTER.stat().st_size,
               "recovery_snapshot": str(backup.relative_to(ROOT)), "images_packed": records,
               "authorized_storage_flag_changes": storage_flag_changes,
               "source_url": "https://polyhaven.com/a/grass_medium_02", "license": "CC0-1.0",
               "license_url": "https://polyhaven.com/license",
               "license_record": "art/environment/vision-vegetation-handoff.json",
               "verification_file": str(evidence_path.relative_to(ROOT)), "validation": final_proof,
               "change_scope": "Four original image byte buffers embedded, plus use_fake_user=True on two original unused materials solely to retain them. Geometry, topology, UV, object transforms, all material nodes/links/inputs, image pixel values, colorspaces, layout, lights, cameras, actions and all other packed images unchanged.",
               "historical_handoffs_untouched": True, "live_unsaved_zombie_untouched": True,
               "fbx_exported": False}
    HANDOFF.write_text(json.dumps(handoff, ensure_ascii=False, indent=2))
    print("ENVIRONMENT PORTABLE PACK PASS", json.dumps({"before": EXPECTED_SHA, "after": final_sha,
          "file_bytes": MASTER.stat().st_size, "packed_images": final_proof["dependencies"]["packed_image_count"]}))


def verify_portable_source():
    """Verify the frozen packed source from a fresh process without saving the blend."""
    handoff = json.loads(HANDOFF.read_text())
    evidence_path = ROOT / handoff["verification_file"]
    evidence = json.loads(evidence_path.read_text())
    if not bpy.app.background or Path(bpy.data.filepath).resolve() != MASTER:
        raise RuntimeError("Fresh verification requires the formal Environment in background")
    if hash_file(MASTER) != handoff["master_sha256"]:
        raise AssertionError("Environment changed after its portable source handoff")
    proof = {"passed": True, "source_sha256": handoff["master_sha256"],
             "protected_counts": verify_source_state(evidence["expected_after"]),
             "dependencies": inspect_portable_dependencies(), "blend_saved": False}
    for record in handoff["images_packed"]:
        verify_packed_image(record)
    proof["original_encoded_bytes_and_decoded_float32_pixels_unchanged"] = True
    evidence["fresh_process_verification"] = proof
    evidence_path.write_text(json.dumps(evidence, ensure_ascii=False, indent=2))
    handoff["fresh_process_verification"] = proof
    HANDOFF.write_text(json.dumps(handoff, ensure_ascii=False, indent=2))
    print("ENVIRONMENT FRESH PROCESS VERIFY PASS", json.dumps(proof))


if __name__ == "__main__":
    if "--verify-only" in sys.argv:
        verify_portable_source()
    else:
        run_portable_packing()
