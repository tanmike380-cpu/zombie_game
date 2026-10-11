"""Regression checks for truthful, non-destructive concept review delivery."""

import hashlib
import tempfile
import unittest
from pathlib import Path
from unittest.mock import patch

from PIL import Image

from tools.art import concept_palette_review as review


class PaletteReviewTests(unittest.TestCase):
    """Keep partial delivery, previous art and the shared HQ footprint explicit."""

    def setUp(self):
        self.temp_dir = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp_dir.cleanup)
        self.project_root = Path(self.temp_dir.name)
        self.revision_root = self.project_root / "revision"
        self.revision_root.mkdir()
        source_path = self.project_root / "old.png"
        output_path = self.revision_root / "front.png"
        Image.new("RGB", (180, 180), "gray").save(source_path)
        Image.new("RGB", (180, 180), "silver").save(output_path)
        self.view = {"name": "front", "source": "old.png", "path": "front.png",
                     "width": 180, "height": 180,
                     "source_sha256": hashlib.sha256(source_path.read_bytes()).hexdigest(),
                     "sha256": hashlib.sha256(output_path.read_bytes()).hexdigest()}
        self.asset = {"id": "sample", "prompt": "Preserve shape, adjust shadows only.",
                      "views": [self.view], "visual_review": {"checked": True}}
        for attribute, value in (("PROJECT_ROOT", self.project_root),
                                 ("REVISION_ROOT", self.revision_root)):
            patcher = patch.object(review, attribute, value)
            patcher.start()
            self.addCleanup(patcher.stop)

    def validate(self, assets, blocked, expected):
        """Validate fixtures against a small explicit expected asset inventory."""
        with patch.object(review, "load_expected_views", return_value=expected):
            review.validate_assets(assets, blocked)

    def test_complete_revision_passes(self):
        self.validate([self.asset], [], {"sample": {"front"}})

    def test_missing_view_fails(self):
        with self.assertRaisesRegex(ValueError, "Incomplete"):
            self.validate([self.asset], [], {"sample": {"front", "side"}})

    def test_source_mutation_fails(self):
        Image.new("RGB", (180, 180), "blue").save(self.project_root / "old.png")
        with self.assertRaisesRegex(ValueError, "hash mismatch"):
            self.validate([self.asset], [], {"sample": {"front"}})

    def test_disclosed_blocked_asset_is_not_claimed_complete(self):
        blocked = [{"id": "blocked", "reason": "Image generation failed; no output.",
                    "status": "blocked-no-output"}]
        self.validate([self.asset], blocked, {"sample": {"front"}, "blocked": {"front"}})
        review.save_review([self.asset], blocked)
        self.assertIn("partial-awaiting-user-review", (self.revision_root / "manifest.json").read_text())
        self.assertIn("未完成", (self.revision_root / "review.html").read_text())

    def test_blocked_asset_cannot_also_be_completed(self):
        blocked = [{"id": "sample", "reason": "no output", "status": "blocked-no-output"}]
        with self.assertRaisesRegex(ValueError, "also reported"):
            self.validate([self.asset], blocked, {"sample": {"front"}})

    def test_approved_alternative_preserves_failure_as_history(self):
        self.asset.update(replaces_blocked_attempt=True,
                          user_approved_alternative="Non-gory Western clothing")
        failed = {"id": "sample", "reason": "no output", "status": "blocked-no-output"}
        assets, blocked = review.reconcile_approved_alternatives([self.asset], [failed])
        self.assertEqual(blocked, [])
        self.assertEqual(assets[0]["prior_failed_attempts"], [failed])
        self.validate(assets, blocked, {"sample": {"front"}})

    def test_unapproved_alternative_does_not_hide_block(self):
        self.asset["replaces_blocked_attempt"] = True
        failed = {"id": "sample", "reason": "no output", "status": "blocked-no-output"}
        _, blocked = review.reconcile_approved_alternatives([self.asset], [failed])
        self.assertEqual(blocked, [failed])

    def test_hq_requires_shared_five_by_five_footprint(self):
        self.asset["id"] = "buildings/byzantine/01-hq-age1"
        self.asset["footprint_tiles"] = [3, 3]
        expected = {self.asset["id"]: {"front"}}
        with self.assertRaisesRegex(ValueError, "5×5"):
            self.validate([self.asset], [], expected)
        self.asset["footprint_tiles"] = [5, 5]
        self.validate([self.asset], [], expected)

    def test_paths_cannot_escape_revision_root(self):
        self.view["path"] = "../old.png"
        with self.assertRaisesRegex(ValueError, "out-of-root"):
            self.validate([self.asset], [], {"sample": {"front"}})

    def test_missing_assets_cannot_be_silently_omitted(self):
        with self.assertRaisesRegex(ValueError, "roster mismatch"):
            self.validate([self.asset], [], {"sample": {"front"}, "missing": {"front"}})


if __name__ == "__main__":
    unittest.main()
