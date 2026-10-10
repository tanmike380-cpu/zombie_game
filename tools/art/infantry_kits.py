"""Publish complete, reviewed infantry equipment turnarounds without replacing old PNGs."""

import hashlib
import json
import shutil

from PIL import Image

from tools.art.concept_views import CONCEPT_ROOT, EXPORT_ROOT


KIT_DIRECTORY = "2026-10-10-infantry-kits"
INFANTRY_KINDS = {
    "tianchao": ("archer", "repeating-crossbowman", "crossbowman", "three-eyed-handgunner"),
    "byzantine": ("archer", "crossbowman", "javelin-legionary", "matchlock-infantry"),
}
INFANTRY_RANKS = ("militia", "veteran", "elite")


def get_kit_ids():
    """Return all approved faction/type/rank identifiers for this concept revision."""
    return {f"humans/{faction}/{kind}-kit-v1/{rank}"
            for faction, kinds in INFANTRY_KINDS.items()
            for kind in kinds for rank in INFANTRY_RANKS}


def get_base_ids():
    """Return the six prior unarmed body sources retained as historical inputs."""
    return {f"humans/{faction}/base/{rank}"
            for faction in INFANTRY_KINDS for rank in INFANTRY_RANKS}


def check_kit_roster(assets):
    """Reject incomplete rosters, duplicate IDs, or mismatched faction/type/rank metadata."""
    if len(assets) != len(get_kit_ids()) or {asset["id"] for asset in assets} != get_kit_ids():
        raise ValueError("Expected all eight infantry types × three ranks, with unique kit IDs")
    for asset in assets:
        expected_id = f"humans/{asset['faction']}/{asset['unit_kind']}-kit-v1/{asset['rank']}"
        if asset["category"] != "human" or asset["id"] != expected_id or not asset.get("accessory"):
            raise ValueError(f"Incorrect infantry kit metadata: {asset['id']}")


def build_kit_manifest(current_manifest, kits):
    """Activate equipped variants while preserving every older body and reference record."""
    from tools.art.concept_review import check_asset_inventory

    check_kit_roster(kits)
    retained_assets = [asset for asset in current_manifest["assets"]
                       if asset["category"] != "human"]
    old_bodies = [asset for asset in current_manifest["assets"]
                  if asset["category"] == "human" and asset["id"] not in get_kit_ids()]
    archive_by_id = {asset["id"]: asset
                     for asset in current_manifest.get("archived_assets", []) + old_bodies}
    updated_manifest = dict(current_manifest, version=max(4, current_manifest.get("version", 1)), assets=retained_assets + kits,
                            archived_assets=list(archive_by_id.values()))
    check_asset_inventory(updated_manifest)
    return updated_manifest


def check_kit_sources(assets, concept_root):
    """Check all source images before copying any part of the new upload catalog."""
    from tools.art.concept_review import check_asset_views

    for asset in assets:
        check_asset_views(asset)
        for view in asset["views"]:
            expected_source = (f"{KIT_DIRECTORY}/{asset['faction']}/{asset['unit_kind']}/"
                               f"{asset['rank']}/{view['name']}.png")
            if view["source"] != expected_source or not view.get("copy_source"):
                raise ValueError(f"Unexpected kit source: {asset['id']}/{view['name']}")
            source_path = concept_root / expected_source
            with Image.open(source_path) as source_image:
                source_image.load()
                if source_image.size != (view["width"], view["height"]) or min(source_image.size) < 150:
                    raise ValueError(f"Invalid kit source dimensions: {source_path}")
            if hashlib.sha256(source_path.read_bytes()).hexdigest() != view["sha256"]:
                raise ValueError(f"Kit source hash mismatch: {source_path}")


def copy_kit_views(assets, concept_root, export_root):
    """Copy approved PNGs byte-for-byte; refuse to overwrite a different existing image."""
    copy_pairs = []
    for asset in assets:
        for view in asset["views"]:
            source_path = concept_root / view["source"]
            output_path = export_root / asset["id"] / (view["name"] + ".png")
            if output_path.exists() and output_path.read_bytes() != source_path.read_bytes():
                raise FileExistsError(f"Refusing to overwrite prior kit image: {output_path}")
            copy_pairs.append((source_path, output_path))
    for source_path, output_path in copy_pairs:
        output_path.parent.mkdir(parents=True, exist_ok=True)
        shutil.copyfile(source_path, output_path)


def publish_kits():
    """Publish only after both factions deliver the complete checked 72-image set."""
    from tools.art.concept_review import validate_delivery
    from tools.art.modular_infantry import load_fragment

    kits = []
    for faction in INFANTRY_KINDS:
        kits.extend(load_fragment(CONCEPT_ROOT / KIT_DIRECTORY / faction / "manifest.json"))
    kits.extend(load_fragment(CONCEPT_ROOT / KIT_DIRECTORY / "tianchao/manifest-additional.json"))
    manifest_path = EXPORT_ROOT / "manifest.json"
    updated_manifest = build_kit_manifest(json.loads(manifest_path.read_text()), kits)
    check_kit_sources(kits, CONCEPT_ROOT)
    copy_kit_views(kits, CONCEPT_ROOT, EXPORT_ROOT)
    manifest_path.write_text(json.dumps(updated_manifest, ensure_ascii=False, indent=2) + "\n")
    validate_delivery()


if __name__ == "__main__":
    publish_kits()
