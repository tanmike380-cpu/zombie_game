"""Regression checks for three-view weapons and preservation of historical images."""

import copy
import unittest

from tools.art.concept_review import check_asset_inventory, check_asset_views
from tools.art.infantry_kits import build_kit_manifest
from tools.art.test_infantry_kits import make_kit_delivery
from tools.art.test_modular_infantry import make_asset
from tools.art.weapon_views import build_weapon_manifest, get_weapon_ids


def make_weapon_delivery():
    """Build metadata-only fixtures, never substitute artwork for generated references."""
    previous, kits = make_kit_delivery()
    previous = build_kit_manifest(previous, kits)
    weapons = []
    for original_id in sorted(get_weapon_ids()):
        asset = make_asset(original_id + "-views-v2", "weapon")
        asset.update(previous_id=original_id, faction=original_id.split("/")[1])
        asset["views"] = [{"name": name} for name in ("front", "side", "back")]
        weapons.append(asset)
    return previous, kits, weapons


class WeaponViewChecks(unittest.TestCase):
    def test_complete_catalog_preserves_prior_weapons_and_people(self):
        previous, kits, weapons = make_weapon_delivery()
        before = copy.deepcopy(previous)
        result = build_weapon_manifest(previous, weapons)
        self.assertEqual(previous, before)
        self.assertEqual(result["version"], 5)
        self.assertEqual(len(result["assets"]), 99)
        self.assertEqual(len(result["archived_assets"]), 44)
        self.assertEqual(sum(len(asset["views"]) for asset in result["assets"]), 297)
        self.assertEqual(build_weapon_manifest(result, weapons), result)
        self.assertEqual(build_kit_manifest(result, kits)["version"], 5)

    def test_missing_or_duplicate_weapon_rejected(self):
        previous, _, weapons = make_weapon_delivery()
        with self.assertRaises(ValueError):
            build_weapon_manifest(previous, weapons[:-1])
        weapons[-1] = weapons[0]
        with self.assertRaises(ValueError):
            build_weapon_manifest(previous, weapons)

    def test_invalid_views_and_faction_rejected(self):
        previous, _, weapons = make_weapon_delivery()
        weapons[0]["views"][2]["name"] = "side"
        with self.assertRaises(ValueError):
            check_asset_views(weapons[0])
        weapons[0]["views"][2]["name"] = "back"
        weapons[0]["faction"] = "wrong"
        with self.assertRaises(ValueError):
            build_weapon_manifest(previous, weapons)

    def test_previous_catalog_cannot_be_lost(self):
        previous, _, weapons = make_weapon_delivery()
        result = build_weapon_manifest(previous, weapons)
        result["archived_assets"] = [asset for asset in result["archived_assets"]
                                      if asset["id"] != "weapons/tianchao/bow"]
        with self.assertRaises(ValueError):
            check_asset_inventory(result)


if __name__ == "__main__":
    unittest.main()
