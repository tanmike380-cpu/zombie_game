"""Publish footprint-correct tower references into new folders, retaining prior art."""

import copy
import hashlib
import json
import shutil

from PIL import Image

from tools.art.concept_review import check_asset_inventory, check_asset_views, check_view_file
from tools.art.concept_views import CONCEPT_ROOT, EXPORT_ROOT
from tools.art.modular_infantry import load_fragment

TOWER_FOOTPRINTS = {
    "buildings/tianchao/26-arrow-tower": [2, 2],
    "buildings/tianchao/27-cannon-tower": [2, 2],
    "buildings/tianchao/28-great-wall": [3, 2],
    "buildings/byzantine/26-arrow-tower": [2, 2],
    "buildings/byzantine/27-cannon-tower": [2, 2],
    "buildings/byzantine/29-flame-tower": [2, 2],
}


def check_tower_roster(towers):
    """Require all six new variants, exact tile dimensions, and independent views."""
    if len(towers) != 6 or {tower.get("previous_id") for tower in towers} != set(TOWER_FOOTPRINTS):
        raise ValueError("Expected six footprint tower variants with their prior IDs")
    for tower in towers:
        prior_id = tower["previous_id"]
        if tower["id"] != prior_id + "-footprint-v2" or tower["category"] != "building":
            raise ValueError(f"Tower must use a new building folder: {tower['id']}")
        if tower.get("footprint_tiles") != TOWER_FOOTPRINTS[prior_id]:
            raise ValueError(f"Wrong width/depth tile footprint: {tower['id']}")
        check_asset_views(tower)


def build_tower_manifest(current_manifest, towers):
    """Keep original tower records in the archive; activate only new versioned IDs."""
    check_tower_roster(towers)
    previous_ids = set(TOWER_FOOTPRINTS)
    new_ids = {tower["id"] for tower in towers}
    current_assets = current_manifest["assets"]
    archive = list(current_manifest.get("archived_assets", []))
    archive += [asset for asset in current_assets if asset["id"] in previous_ids]
    archive_by_id = {asset["id"]: asset for asset in archive}
    if not previous_ids.issubset(archive_by_id):
        raise ValueError("Cannot publish towers without preserving all six original references")
    retained = [asset for asset in current_assets if asset["id"] not in previous_ids | new_ids]
    updated_manifest = dict(current_manifest, version=3, assets=retained + copy.deepcopy(towers),
                            archived_assets=list(archive_by_id.values()))
    check_asset_inventory(updated_manifest)
    return updated_manifest


def check_tower_sources(towers):
    """Verify every delivered source before copying any new output files."""
    for tower in towers:
        for view in tower["views"]:
            source_path = CONCEPT_ROOT / view["source"]
            if not view.get("copy_source"):
                raise ValueError(f"Tower view must copy its reviewed source: {source_path}")
            with Image.open(source_path) as source:
                source.load()
                if source.size != (view["width"], view["height"]) or min(source.size) < 150:
                    raise ValueError(f"Tower source dimensions differ: {source_path}")
            if hashlib.sha256(source_path.read_bytes()).hexdigest() != view["sha256"]:
                raise ValueError(f"Tower source hash differs: {source_path}")


def copy_tower_sources(towers):
    """Copy images only to new version folders; reject a conflicting existing image."""
    for tower in towers:
        for view in tower["views"]:
            source_path = CONCEPT_ROOT / view["source"]
            output_path = EXPORT_ROOT / tower["id"] / (view["name"] + ".png")
            if output_path.exists() and output_path.read_bytes() != source_path.read_bytes():
                raise FileExistsError(f"Preserve the existing tower version: {output_path}")
            output_path.parent.mkdir(parents=True, exist_ok=True)
            shutil.copyfile(source_path, output_path)


def publish_towers():
    """Validate the new tower delivery and publish a catalog retaining older images."""
    source_root = CONCEPT_ROOT / "2026-10-05-square-towers"
    towers = load_fragment(source_root / "tianchao/manifest.json")
    towers += load_fragment(source_root / "byzantine/manifest.json")
    manifest_path = EXPORT_ROOT / "manifest.json"
    updated_manifest = build_tower_manifest(json.loads(manifest_path.read_text()), towers)
    check_tower_sources(towers)
    copy_tower_sources(towers)
    for asset in check_asset_inventory(updated_manifest):
        for view in asset["views"]:
            check_view_file(asset, view, EXPORT_ROOT, CONCEPT_ROOT)
    manifest_path.write_text(json.dumps(updated_manifest, ensure_ascii=False, indent=2) + "\n")
    print("Published six footprint tower variants; all prior tower images retained")


if __name__ == "__main__":
    publish_towers()
