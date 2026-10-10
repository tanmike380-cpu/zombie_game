"""Regression checks for the equipped infantry roster and preservation of prior concepts."""

import copy
from pathlib import Path
import tempfile
import unittest

from tools.art.concept_review import check_asset_inventory, check_asset_views
from tools.art.infantry_kits import (
    INFANTRY_KINDS, INFANTRY_RANKS, build_kit_manifest, copy_kit_views, get_base_ids,
)
from tools.art.modular_infantry import build_manifest
from tools.art.test_modular_infantry import make_asset, make_tower_delivery
from tools.art.tower_concepts import build_tower_manifest


def make_kit_delivery():
    """Build metadata fixtures only; these fixtures contain no deliverable artwork."""
    previous, towers, _, _ = make_tower_delivery()
    previous = build_tower_manifest(previous, towers)
    kits = []
    for faction, kinds in INFANTRY_KINDS.items():
        for kind in kinds:
            for rank in INFANTRY_RANKS:
                asset = make_asset(f"humans/{faction}/{kind}-kit-v1/{rank}", "human")
                asset.update(faction=faction, unit_kind=kind, rank=rank, accessory="test fixture")
                kits.append(asset)
    return previous, kits


class InfantryKitChecks(unittest.TestCase):
    def test_latest_roster_keeps_all_historical_references(self):
        previous, kits = make_kit_delivery()
        original_copy = copy.deepcopy(previous)
        result = build_kit_manifest(previous, kits)
        self.assertEqual(previous, original_copy)
        self.assertEqual(result["version"], 4)
        self.assertEqual(len(result["assets"]), 99)
        self.assertEqual(len(result["archived_assets"]), 36)
        self.assertEqual(sum(len(asset["views"]) for asset in result["assets"]), 281)
        self.assertTrue(get_base_ids() <= {asset["id"] for asset in result["archived_assets"]})
        self.assertEqual(build_kit_manifest(result, kits), result)

    def test_missing_unit_rank_or_duplicate_is_rejected(self):
        previous, kits = make_kit_delivery()
        with self.assertRaises(ValueError):
            build_kit_manifest(previous, kits[:-1])
        kits[-1] = kits[0]
        with self.assertRaises(ValueError):
            build_kit_manifest(previous, kits)

    def test_incorrect_rank_metadata_is_rejected(self):
        previous, kits = make_kit_delivery()
        kits[0]["rank"] = "elite"
        with self.assertRaisesRegex(ValueError, "metadata"):
            build_kit_manifest(previous, kits)

    def test_unarmed_source_cannot_be_lost_or_reactivated(self):
        previous, kits = make_kit_delivery()
        result = build_kit_manifest(previous, kits)
        with self.assertRaisesRegex(ValueError, "base-only"):
            build_manifest(result, [], [])
        result["archived_assets"] = [asset for asset in result["archived_assets"]
                                      if asset["id"] != "humans/tianchao/base/militia"]
        with self.assertRaises(ValueError):
            check_asset_inventory(result)

    def test_turnarounds_require_three_distinct_views(self):
        _, kits = make_kit_delivery()
        for asset in kits:
            check_asset_views(asset)
        kits[0]["views"][2]["name"] = "side"
        with self.assertRaises(ValueError):
            check_asset_views(kits[0])

    def test_output_conflict_is_detected_before_copying_any_files(self):
        with tempfile.TemporaryDirectory(prefix="kit-copy-check-") as folder:
            root = Path(folder)
            source_root, export_root = root / "sources", root / "exports"
            source_root.mkdir()
            (source_root / "first.png").write_bytes(b"first test fixture")
            (source_root / "second.png").write_bytes(b"second test fixture")
            conflicting_output = export_root / "fixture" / "side.png"
            conflicting_output.parent.mkdir(parents=True)
            conflicting_output.write_bytes(b"prior approved image fixture")
            assets = [{"id": "fixture", "views": [
                {"name": "front", "source": "first.png"},
                {"name": "side", "source": "second.png"}]}]
            with self.assertRaises(FileExistsError):
                copy_kit_views(assets, source_root, export_root)
            self.assertFalse((export_root / "fixture" / "front.png").exists())
            self.assertEqual(conflicting_output.read_bytes(), b"prior approved image fixture")


if __name__ == "__main__":
    unittest.main()
