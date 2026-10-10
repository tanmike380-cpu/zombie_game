"""Publish independent weapon angles while keeping the original single-view references."""

import hashlib
import json

from PIL import Image

from tools.art.concept_views import CONCEPT_ROOT, EXPORT_ROOT
from tools.art.infantry_kits import copy_kit_views


WEAPON_DIRECTORY = "2026-10-10-weapon-views"
WEAPON_KINDS = {
    "tianchao": ("bow", "repeating-crossbow", "crossbow", "three-eyed-handgun"),
    "byzantine": ("javelin", "bow", "crossbow", "matchlock"),
}


def get_weapon_ids():
    """Return original IDs retained as historical single-view references."""
    return {f"weapons/{faction}/{kind}" for faction, kinds in WEAPON_KINDS.items() for kind in kinds}


def check_weapon_roster(weapons):
    """Reject incomplete rosters, mismatched faction metadata and missing independent views."""
    from tools.art.concept_review import check_asset_views

    expected_ids = {asset_id + "-views-v2" for asset_id in get_weapon_ids()}
    if len(weapons) != 8 or {asset["id"] for asset in weapons} != expected_ids:
        raise ValueError("Expected eight weapons, each with independent front, side and back")
    for asset in weapons:
        if (asset["category"] != "weapon" or asset["previous_id"] + "-views-v2" != asset["id"]
                or asset["id"].split("/")[1] != asset["faction"]):
            raise ValueError(f"Incorrect weapon view metadata: {asset['id']}")
        check_asset_views(asset)


def build_weapon_manifest(current_manifest, weapons):
    """Activate checked turnarounds without losing old artwork or current infantry kits."""
    from tools.art.concept_review import check_asset_inventory

    if current_manifest.get("version", 1) < 4:
        raise ValueError("Publish the complete infantry kits before the weapon angle catalog")
    check_weapon_roster(weapons)
    retained = [asset for asset in current_manifest["assets"] if asset["category"] != "weapon"]
    previous = [asset for asset in current_manifest["assets"] if asset["id"] in get_weapon_ids()]
    archive = {asset["id"]: asset for asset in current_manifest.get("archived_assets", []) + previous}
    result = dict(current_manifest, version=5, assets=retained + weapons, archived_assets=list(archive.values()))
    check_asset_inventory(result)
    return result


def check_weapon_sources(weapons, concept_root):
    """Verify source paths, image dimensions and digests before any publication writes."""
    for asset in weapons:
        original_id = asset["previous_id"]
        folder = original_id.removeprefix("weapons/")
        for view in asset["views"]:
            expected_source = f"{WEAPON_DIRECTORY}/{folder}/{view['name']}.png"
            if view["source"] != expected_source or not view.get("copy_source"):
                raise ValueError(f"Unexpected weapon source: {asset['id']}/{view['name']}")
            source_path = concept_root / expected_source
            with Image.open(source_path) as source_image:
                source_image.load()
                if source_image.size != (view["width"], view["height"]) or min(source_image.size) < 150:
                    raise ValueError(f"Invalid weapon dimensions: {source_path}")
            if hashlib.sha256(source_path.read_bytes()).hexdigest() != view["sha256"]:
                raise ValueError(f"Weapon source hash mismatch: {source_path}")


def publish_weapon_views():
    """Publish only the complete reviewed 24-file fragment, then validate all archives."""
    from tools.art.concept_review import validate_delivery
    from tools.art.modular_infantry import load_fragment

    weapons = load_fragment(CONCEPT_ROOT / WEAPON_DIRECTORY / "manifest.json")
    manifest_path = EXPORT_ROOT / "manifest.json"
    result = build_weapon_manifest(json.loads(manifest_path.read_text()), weapons)
    check_weapon_sources(weapons, CONCEPT_ROOT)
    copy_kit_views(weapons, CONCEPT_ROOT, EXPORT_ROOT)
    manifest_path.write_text(json.dumps(result, ensure_ascii=False, indent=2) + "\n")
    validate_delivery()


if __name__ == "__main__":
    publish_weapon_views()
