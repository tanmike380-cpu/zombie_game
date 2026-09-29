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
                     for asset in manifest["assets"] for view in asset["views"]}
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


def validate_delivery():
    """Check counts, every source, dimensions, unique IDs and output hashes."""
    manifest = json.loads((EXPORT_ROOT / "manifest.json").read_text())
    assets = manifest["assets"]
    if len(assets) != 91 or len({asset["id"] for asset in assets}) != 91:
        raise ValueError("Expected 91 unique model variants")
    category_counts = Counter(asset["id"].split("/")[0] for asset in assets)
    if category_counts != {"humans": 24, "zombies": 7, "siege": 6, "buildings": 54}:
        raise ValueError(f"Wrong category totals: {category_counts}")
    expected_paths = set()
    for asset in assets:
        expected_views = {"front-perspective", "side", "rear-perspective"} if asset["category"] == "building" else {"front", "side", "back"}
        if len(asset["views"]) != 3 or {view["name"] for view in asset["views"]} != expected_views:
            raise ValueError(f"Missing or duplicate views: {asset['id']}")
        for view in asset["views"]:
            source_path = CONCEPT_ROOT / view["source"]
            if not source_path.is_file():
                raise FileNotFoundError(source_path)
            output_path = EXPORT_ROOT / asset["id"] / (view["name"] + ".png")
            expected_paths.add(output_path)
            with Image.open(output_path) as output:
                output.load()
                if output.size != (view["width"], view["height"]):
                    raise ValueError(f"Dimension mismatch: {output_path}")
                if min(output.size) < 150:
                    raise ValueError(f"Suspiciously small panel: {output_path}")
            digest = hashlib.sha256(output_path.read_bytes()).hexdigest()
            if digest != view["sha256"]:
                raise ValueError(f"Hash mismatch: {output_path}")
    actual_paths = set(EXPORT_ROOT.rglob("*.png"))
    if actual_paths != expected_paths:
        raise ValueError(f"Unexpected/missing PNGs: {actual_paths ^ expected_paths}")
    print(f"PASS: {len(assets)} variants, {len(expected_paths)} independent PNGs; dimensions and hashes verified")


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
