"""Apply visual-review crop fixes and validate the complete Tripo delivery."""

import argparse
from collections import Counter
import hashlib
import json

from PIL import Image

from tools.art.concept_views import CONCEPT_ROOT, EXPORT_ROOT, export_crop


def apply_corrections():
    """Replace explicitly reviewed masks and export only the affected views."""
    manifest_path = EXPORT_ROOT / "manifest.json"
    manifest = json.loads(manifest_path.read_text())
    indexed_views = {(asset["id"], view["name"]): view
                     for asset in manifest["assets"] + manifest.get("archived_assets", [])
                     for view in asset["views"]}
    corrections = json.loads((EXPORT_ROOT / "crop-corrections.json").read_text())
    for correction in corrections:
        asset_id, view_name = correction["id"], correction["name"]
        view = indexed_views[(asset_id, view_name)]
        for key in ("polygon", "erase", "exclusions"):
            view.pop(key, None)
        view.update({key: value for key, value in correction.items() if key not in ("id", "name")})
        output_path = EXPORT_ROOT / asset_id / (view_name + ".png")
        view.update(export_crop(CONCEPT_ROOT / view["source"], output_path, view["box"],
                                polygon=view.get("polygon"), erase=view.get("erase")))
    manifest_path.write_text(json.dumps(manifest, ensure_ascii=False, indent=2) + "\n")
    print(f"Applied {len(corrections)} reviewed crop corrections")


def check_asset_inventory(manifest):
    """Verify the approved roster, including retained but inactive human references."""
    assets = manifest["assets"]
    category_counts = Counter(asset["id"].split("/")[0] for asset in assets)
    is_modular = manifest.get("version", 1) >= 2
    expected_counts = {"humans": 6, "weapons": 8, "zombies": 7, "siege": 6, "buildings": 54} if is_modular else {
        "humans": 24, "zombies": 7, "siege": 6, "buildings": 54}
    if category_counts != expected_counts:
        raise ValueError(f"Wrong category totals: {category_counts}")
    if is_modular:
        expected_bodies = {f"humans/{faction}/base/{rank}"
                           for faction in ("tianchao", "byzantine") for rank in ("militia", "veteran", "elite")}
        if {asset["id"] for asset in assets if asset["category"] == "human"} != expected_bodies:
            raise ValueError("Expected six faction/rank base bodies, not weapon-specific people")
        expected_weapons = {"weapons/tianchao/" + name for name in ("bow", "repeating-crossbow", "crossbow", "three-eyed-handgun")}
        expected_weapons |= {"weapons/byzantine/" + name for name in ("javelin", "bow", "crossbow", "matchlock")}
        if {asset["id"] for asset in assets if asset["category"] == "weapon"} != expected_weapons:
            raise ValueError("Expected the eight authored faction weapon designs")
        archive = manifest.get("archived_assets", [])
        if len(archive) != 24 or any(asset["category"] != "human" for asset in archive):
            raise ValueError("Preserve all 24 prior human reference variants in the inactive archive")
    all_assets = assets + manifest.get("archived_assets", [])
    if len({asset["id"] for asset in all_assets}) != len(all_assets):
        raise ValueError("Duplicate active/archive asset IDs")
    return all_assets


def check_asset_views(asset):
    """Return the expected independent view names for one asset category."""
    if asset["category"] == "weapon":
        expected_views = {"reference"}
    elif asset["category"] == "building":
        expected_views = {"front-perspective", "side", "rear-perspective"}
    else:
        expected_views = {"front", "side", "back"}
    if len(asset["views"]) != len(expected_views) or {view["name"] for view in asset["views"]} != expected_views:
        raise ValueError(f"Missing or duplicate views: {asset['id']}")


def check_view_file(asset, view, export_root, concept_root):
    """Validate one referenced PNG against its source, recorded size and digest."""
    source_path = concept_root / view["source"]
    if not source_path.is_file():
        raise FileNotFoundError(source_path)
    output_path = export_root / asset["id"] / (view["name"] + ".png")
    with Image.open(output_path) as output:
        output.load()
        if output.size != (view["width"], view["height"]):
            raise ValueError(f"Dimension mismatch: {output_path}")
        if min(output.size) < 150:
            raise ValueError(f"Suspiciously small panel: {output_path}")
    output_bytes = output_path.read_bytes()
    digest = hashlib.sha256(output_bytes).hexdigest()
    if digest != view["sha256"]:
        raise ValueError(f"Hash mismatch: {output_path}")
    if view.get("copy_source") and output_bytes != source_path.read_bytes():
        raise ValueError(f"Copied reference differs from its source: {output_path}")
    return output_path


def validate_delivery(export_root=EXPORT_ROOT, concept_root=CONCEPT_ROOT):
    """Check active and historical files without making archived people upload targets."""
    manifest = json.loads((export_root / "manifest.json").read_text())
    all_assets = check_asset_inventory(manifest)
    expected_paths = set()
    active_pngs = sum(len(asset["views"]) for asset in manifest["assets"])
    for asset in all_assets:
        check_asset_views(asset)
        for view in asset["views"]:
            expected_paths.add(check_view_file(asset, view, export_root, concept_root))
    actual_paths = set(export_root.rglob("*.png"))
    if actual_paths != expected_paths:
        raise ValueError(f"Unexpected/missing PNGs: {actual_paths ^ expected_paths}")
    print(f"PASS: {len(manifest['assets'])} active variants, {active_pngs} active PNGs, "
          f"{len(expected_paths) - active_pngs} archived PNGs; dimensions and hashes verified")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("command", choices=("correct", "validate"))
    command = parser.parse_args().command
    if command == "correct":
        apply_corrections()
    else:
        validate_delivery()


if __name__ == "__main__":
    main()
