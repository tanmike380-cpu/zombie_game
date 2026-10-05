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


def make_asset(identity, category):
    """Create metadata-only test fixtures; no concept artwork is generated."""
    names = ("reference",) if category == "weapon" else ("front", "side", "back")
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


class ModularInfantryChecks(unittest.TestCase):
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
