"""Checks for scoped HQ concept updates and recoverable reference packaging."""

import copy
import tempfile
import unittest
from pathlib import Path
from unittest.mock import patch

from PIL import Image

from tools.art import headquarters_review as review


class HeadquartersReviewTests(unittest.TestCase):
    """Preserve ordinary concepts, separate view files and the shared 5×5 plot."""

    def setUp(self):
        self.temp_dir = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp_dir.cleanup)
        self.project_root = Path(self.temp_dir.name)
        self.concept_root = self.project_root / "art/concepts"
        self.revision_root = self.concept_root / "warm"
        self.revision_root.mkdir(parents=True)
        for attribute, value in (("PROJECT_ROOT", self.project_root),
                                 ("CONCEPT_ROOT", self.concept_root),
                                 ("REVISION_ROOT", self.revision_root)):
            patcher = patch.object(review, attribute, value)
            patcher.start()
            self.addCleanup(patcher.stop)
        self.assets = []
        for asset_id in sorted(review.HQ_IDS):
            faction, age_name = asset_id.split("/")[1:]
            views = []
            for name in sorted(review.VIEW_NAMES):
                source_path = self.project_root / "old" / faction / age_name / (name + ".png")
                source_path.parent.mkdir(parents=True, exist_ok=True)
                Image.new("RGB", (180, 180), "gray").save(source_path)
                image_path = self.revision_root / faction / age_name / (name + ".png")
                image_path.parent.mkdir(parents=True, exist_ok=True)
                Image.new("RGB", (180, 180), "brown").save(image_path)
                views.append({"name": name, "source": source_path.relative_to(self.project_root).as_posix(),
                              "source_sha256": review.hash_file(source_path),
                              "path": image_path.relative_to(self.revision_root).as_posix(),
                              "sha256": review.hash_file(image_path), "width": 180, "height": 180})
            self.assets.append({"id": asset_id, "footprint_tiles": [5, 5],
                                "prompt": "Keep plot and normal human doors.", "visual_review": {"checked": True},
                                "views": views})
        for faction in ("byzantine", "tianchao"):
            review.save_json(self.revision_root / faction / "fragment.json",
                             {"assets": [asset for asset in self.assets if f"/{faction}/" in asset["id"]]})

    def test_complete_inventory_passes(self):
        review.validate_assets(self.assets)

    def test_plot_change_rejected(self):
        self.assets[0]["footprint_tiles"] = [3, 3]
        with self.assertRaisesRegex(ValueError, "5×5"):
            review.validate_assets(self.assets)

    def test_duplicate_asset_rejected(self):
        self.assets[-1] = copy.deepcopy(self.assets[0])
        with self.assertRaisesRegex(ValueError, "six"):
            review.validate_assets(self.assets)

    def test_missing_side_rejected(self):
        self.assets[0]["views"].pop()
        with self.assertRaisesRegex(ValueError, "angle"):
            review.validate_assets(self.assets)

    def test_changed_image_rejected(self):
        image_path = self.revision_root / self.assets[0]["views"][0]["path"]
        Image.new("RGB", (180, 180), "blue").save(image_path)
        with self.assertRaisesRegex(ValueError, "hash"):
            review.validate_assets(self.assets)

    def test_references_survive_removal_of_old_active_path(self):
        old_paths = [self.project_root / view["source"] for asset in self.assets for view in asset["views"]]
        review.freeze_references(self.assets)
        for old_path in old_paths:
            old_path.unlink()
        review.validate_assets(review.load_assets())

    def test_reference_copy_refuses_overwrite(self):
        source_path = self.project_root / self.assets[0]["views"][0]["source"]
        target_path = self.project_root / "protected.png"
        Image.new("RGB", (180, 180), "blue").save(target_path)
        with self.assertRaisesRegex(ValueError, "differs"):
            review.copy_checked(source_path, target_path)

    def test_nested_generation_input_survives_source_removal(self):
        original_view = self.assets[0]["views"][0]
        record = {"revisions": [{"inputs": [{"source": original_view["source"],
                                             "source_sha256": original_view["source_sha256"]}]}]}
        old_path = self.project_root / original_view["source"]
        review.freeze_generation_inputs(record)
        old_path.unlink()
        frozen_input = record["revisions"][0]["inputs"][0]
        self.assertEqual(review.hash_file(self.project_root / frozen_input["source"]), original_view["source_sha256"])
        self.assertEqual(frozen_input["original_source"], original_view["source"])

    def test_unrelated_metadata_change_rejected(self):
        original = {"assets": [{"id": "humans/sample", "views": [], "label": "old"}], "archived_assets": []}
        changed = copy.deepcopy(original)
        changed["assets"][0]["label"] = "new"
        with self.assertRaisesRegex(ValueError, "Unrelated"):
            review.validate_unaffected_catalog(original, changed)

    def test_archived_inventory_change_rejected(self):
        original = {"assets": [], "archived_assets": []}
        changed = {"assets": [], "archived_assets": [{"id": "removed-history"}]}
        with self.assertRaisesRegex(ValueError, "Historical"):
            review.validate_unaffected_catalog(original, changed)

    def test_activation_preserves_previous_hqs_and_other_images(self):
        catalog_assets = []
        for asset in self.assets:
            old_views = []
            for view in asset["views"]:
                source_path = self.project_root / view["source"]
                old_path = self.concept_root / "tripo" / asset["id"] / (view["name"] + ".png")
                review.copy_checked(source_path, old_path)
                old_views.append({"name": view["name"], "sha256": view["source_sha256"]})
            catalog_assets.append({"id": asset["id"], "views": old_views})
        person_path = self.concept_root / "tripo/humans/sample/front.png"
        person_path.parent.mkdir(parents=True)
        Image.new("RGB", (180, 180), "green").save(person_path)
        person_asset = {"id": "humans/sample", "views": [{"name": "front", "sha256": review.hash_file(person_path)}]}
        catalog_assets.append(person_asset)
        catalog_path = self.concept_root / "tripo/manifest.json"
        review.save_json(catalog_path, {"assets": catalog_assets, "archived_assets": []})
        with patch.object(review, "CATALOG_PATH", catalog_path):
            review.activate_headquarters(self.assets)
        updated_catalog = review.read_json(catalog_path)
        self.assertEqual(updated_catalog["assets"][-1], person_asset)
        self.assertEqual(review.hash_file(person_path), person_asset["views"][0]["sha256"])
        for asset in self.assets:
            for view in asset["views"]:
                backup_path = self.revision_root / "previous-warm" / asset["id"] / (view["name"] + ".png")
                self.assertEqual(review.hash_file(backup_path), view["source_sha256"])
                active_path = self.concept_root / "tripo" / asset["id"] / (view["name"] + ".png")
                self.assertEqual(review.hash_file(active_path), view["sha256"])

    def test_review_states_concept_only_and_links_independent_views(self):
        review.publish_review(self.assets)
        review_html = (self.revision_root / "review.html").read_text()
        self.assertIn("Blender和Unity本轮未改", review_html)
        self.assertIn("侧面", review_html)
        self.assertIn("三时代暂定相同5×5占地", review_html)


if __name__ == "__main__":
    unittest.main()
