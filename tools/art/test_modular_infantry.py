"""Regression checks for shared bodies, single-view weapons and inactive references."""

import copy
import hashlib
import io
from pathlib import Path
import tempfile
import unittest

from PIL import Image

from tools.art.concept_review import check_asset_inventory, check_asset_views, check_view_file
from tools.art.modular_infantry import build_manifest
from tools.art.tower_concepts import TOWER_FOOTPRINTS, build_tower_manifest


def make_asset(identity, category):
    """Create metadata-only test fixtures; no concept artwork is generated."""
    names = ("reference",) if category == "weapon" else ("front", "side", "back")
    if category == "building":
        names = ("front-perspective", "side", "rear-perspective")
    return {"id": identity, "category": category, "views": [{"name": name} for name in names]}


def make_catalog():
    """Create both the historical roster and modular fragments for schema testing."""
    retained = [make_asset(f"{group}/{index}", category)
                for group, category, count in (("zombies", "zombie", 7), ("siege", "siege", 6), ("buildings", "building", 54))
                for index in range(count)]
    archive = [make_asset(f"humans/old/{index}", "human") for index in range(24)]
    bodies = [make_asset(f"humans/{faction}/base/{rank}", "human")
              for faction in ("tianchao", "byzantine") for rank in ("militia", "veteran", "elite")]
    weapons = [make_asset(f"weapons/{faction}/{weapon}", "weapon")
               for faction, roster in (("tianchao", ("bow", "repeating-crossbow", "crossbow", "three-eyed-handgun")),
                                       ("byzantine", ("javelin", "bow", "crossbow", "matchlock")))
               for weapon in roster]
    return {"version": 1, "assets": retained + archive}, bodies, weapons


def make_tower_delivery():
    """Create a modular fixture with real tower IDs and six footprint variants."""
    previous, bodies, weapons = make_catalog()
    manifest = build_manifest(previous, bodies, weapons)
    building_assets = [asset for asset in manifest["assets"] if asset["category"] == "building"]
    towers = []
    for original, (identity, footprint) in zip(building_assets, TOWER_FOOTPRINTS.items()):
        original["id"] = identity
        tower = make_asset(identity + "-footprint-v2", "building")
        tower.update(previous_id=identity, footprint_tiles=footprint.copy())
        towers.append(tower)
    return manifest, towers, bodies, weapons


class ModularInfantryChecks(unittest.TestCase):
    def test_tower_versions_preserve_originals_and_are_idempotent(self):
        previous, towers, bodies, weapons = make_tower_delivery()
        original_copy = copy.deepcopy(previous)
        result = build_tower_manifest(previous, towers)
        self.assertEqual(previous, original_copy)
        self.assertEqual(len(result["assets"]), 81)
        self.assertEqual(len(result["archived_assets"]), 30)
        self.assertEqual(build_tower_manifest(result, towers), result)
        republished = build_manifest(result, bodies, weapons)
        self.assertEqual(republished["version"], 3)
        self.assertEqual({asset["id"]: asset for asset in republished["assets"]},
                         {asset["id"]: asset for asset in result["assets"]})
        self.assertEqual(republished["archived_assets"], result["archived_assets"])

    def test_tower_footprint_and_roster_are_required(self):
        previous, towers, _, _ = make_tower_delivery()
        with self.assertRaises(ValueError):
            build_tower_manifest(previous, towers[:-1])
        towers[0]["footprint_tiles"] = [2, 3]
        with self.assertRaisesRegex(ValueError, "Wrong width/depth"):
            build_tower_manifest(previous, towers)

    def test_original_tower_archive_cannot_be_removed(self):
        previous, towers, _, _ = make_tower_delivery()
        result = build_tower_manifest(previous, towers)
        result["archived_assets"].pop()
        with self.assertRaises(ValueError):
            check_asset_inventory(result)

    def test_archive_and_active_counts(self):
        previous, bodies, weapons = make_catalog()
        result = build_manifest(previous, bodies, weapons)
        self.assertEqual(len(result["assets"]), 81)
        self.assertEqual(len(result["archived_assets"]), 24)
        self.assertEqual(sum(len(asset["views"]) for asset in result["assets"]), 227)
        self.assertEqual(result["archived_assets"], previous["assets"][-24:])
        self.assertEqual(previous["version"], 1)
        self.assertEqual(build_manifest(result, bodies, weapons), result)

    def test_missing_rank_or_weapon_is_rejected(self):
        previous, bodies, weapons = make_catalog()
        with self.assertRaises(ValueError):
            build_manifest(previous, bodies[:-1], weapons)
        with self.assertRaises(ValueError):
            build_manifest(previous, bodies, weapons[:-1])

    def test_body_identity_and_archive_are_checked(self):
        previous, bodies, weapons = make_catalog()
        altered = copy.deepcopy(bodies)
        altered[0]["id"] = "humans/tianchao/archer/militia"
        with self.assertRaises(ValueError):
            build_manifest(previous, altered, weapons)
        result = build_manifest(previous, bodies, weapons)
        result["archived_assets"].pop()
        with self.assertRaises(ValueError):
            check_asset_inventory(result)

    def test_weapon_has_one_view_body_has_three(self):
        check_asset_views(make_asset("weapons/tianchao/bow", "weapon"))
        invalid_weapon = make_asset("weapons/tianchao/bow", "human")
        invalid_weapon["category"] = "weapon"
        with self.assertRaises(ValueError):
            check_asset_views(invalid_weapon)
        invalid_body = make_asset("humans/tianchao/base/militia", "human")
        invalid_body["views"][-1]["name"] = "side"
        with self.assertRaises(ValueError):
            check_asset_views(invalid_body)

    def test_copied_source_and_output_match(self):
        image_buffer = io.BytesIO()
        Image.new("RGB", (150, 150), "gray").save(image_buffer, format="PNG")
        png_bytes = image_buffer.getvalue()
        with tempfile.TemporaryDirectory(prefix="concept-validation-") as folder:
            root = Path(folder)
            export_root = root / "tripo"
            source_path = root / "source.png"
            source_path.write_bytes(png_bytes)
            asset = make_asset("weapons/tianchao/bow", "weapon")
            output_path = export_root / asset["id"] / "reference.png"
            output_path.parent.mkdir(parents=True)
            output_path.write_bytes(png_bytes)
            view = dict(name="reference", source="source.png", copy_source=True,
                        width=150, height=150, sha256=hashlib.sha256(png_bytes).hexdigest())
            self.assertEqual(check_view_file(asset, view, export_root, root), output_path)
            view["sha256"] = "invalid"
            with self.assertRaisesRegex(ValueError, "Hash mismatch"):
                check_view_file(asset, view, export_root, root)
            view["sha256"] = hashlib.sha256(png_bytes).hexdigest()
            source_path.write_bytes(png_bytes + b"different source")
            with self.assertRaisesRegex(ValueError, "differs from its source"):
                check_view_file(asset, view, export_root, root)


if __name__ == "__main__":
    unittest.main()
