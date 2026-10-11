"""Protect exact archive scope and reject overwriting recoverable artwork."""

import tempfile
import unittest
from pathlib import Path
from unittest.mock import patch

from tools.art import retire_cool_concepts as retirement


class RetireCoolConceptTests(unittest.TestCase):
    """Check mechanical archive safety without deleting real project assets."""

    def setUp(self):
        self.temp_dir = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp_dir.cleanup)
        self.project_root = Path(self.temp_dir.name)
        self.cold_root = self.project_root / "cold"
        self.cold_root.mkdir()
        self.backup_root = self.project_root / "backup"
        self.warm_root = self.project_root / "warm"
        self.warm_root.mkdir()
        (self.warm_root / "manifest.json").write_text("{}")
        for target, attribute, value in (
            (retirement, "COLD_ROOT", self.cold_root),
            (retirement, "BACKUP_ROOT", self.backup_root),
            (retirement.review, "REVISION_ROOT", self.warm_root),
            (retirement.review, "PROJECT_ROOT", self.project_root),
        ):
            patcher = patch.object(target, attribute, value)
            patcher.start()
            self.addCleanup(patcher.stop)

    def test_inventory_records_exact_bytes(self):
        file_path = self.cold_root / "retained.png"
        file_path.write_bytes(b"unchanged image")
        self.assertEqual(retirement.inventory_files(self.cold_root),
                         {"retained.png": retirement.review.hash_file(file_path)})

    def test_inventory_rejects_external_symlink(self):
        (self.cold_root / "outside-link").symlink_to(self.project_root)
        with self.assertRaisesRegex(ValueError, "symlink"):
            retirement.inventory_files(self.cold_root)

    def test_existing_backup_blocks_move(self):
        self.backup_root.mkdir()
        with patch.object(retirement.review, "load_assets", return_value=[]), \
                patch.object(retirement.review, "validate_assets"):
            with self.assertRaisesRegex(ValueError, "Backup exists"):
                retirement.retire_revision()
        self.assertTrue(self.cold_root.is_dir())

    def test_unpublished_replacement_blocks_move(self):
        (self.warm_root / "manifest.json").unlink()
        with patch.object(retirement.review, "load_assets", return_value=[]), \
                patch.object(retirement.review, "validate_assets"):
            with self.assertRaisesRegex(ValueError, "Publish checked"):
                retirement.retire_revision()
        self.assertTrue(self.cold_root.is_dir())

    def test_archive_verifies_bytes_and_preserves_original_source(self):
        source_path = self.project_root / "original.png"
        source_path.write_bytes(b"original warm pixels")
        reference = {"source": "original.png", "source_sha256": retirement.review.hash_file(source_path)}
        retirement.review.save_json(self.cold_root / "manifest.json", {"assets": [{"views": [reference]}]})
        cold_image = self.cold_root / "candidate.png"
        cold_image.write_bytes(b"cool candidate pixels")
        original_inventory = retirement.inventory_files(self.cold_root)
        with patch.object(retirement.review, "load_assets", return_value=[]), \
                patch.object(retirement.review, "validate_assets"), \
                patch.object(retirement, "preserve_western_common"):
            retirement.retire_revision()
        archive_path = self.backup_root / "original-folder"
        self.assertEqual(retirement.inventory_files(archive_path), original_inventory)
        self.assertEqual(source_path.read_bytes(), b"original warm pixels")
        record = retirement.review.read_json(self.cold_root / "retired.json")
        self.assertEqual(record["archived_files"], 2)
        self.assertTrue((self.cold_root / "review.html").exists())


if __name__ == "__main__":
    unittest.main()
