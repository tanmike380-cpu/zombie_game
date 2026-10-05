"""Publish reviewed body/weapon manifest fragments without deleting old references."""

import json
from tools.art.concept_review import check_asset_inventory, check_asset_views, check_view_file
from tools.art.concept_views import CONCEPT_ROOT, EXPORT_ROOT


def load_fragment(fragment_path):
    """Read the independently reviewed asset records for one workstream."""
    if not fragment_path.is_file():
        raise FileNotFoundError(f"Concept workstream has not delivered its manifest: {fragment_path}")
    return json.loads(fragment_path.read_text())["assets"]


def build_manifest(current_manifest, bodies, weapons):
    """Replace active weapon-specific people with shared bodies; retain their archive."""
    previous_humans = [asset for asset in current_manifest["assets"]
                       if asset["id"].startswith("humans/") and "/base/" not in asset["id"]]
    archive = current_manifest.get("archived_assets", []) + previous_humans
    archive_by_id = {asset["id"]: asset for asset in archive}
    retained_assets = [asset for asset in current_manifest["assets"]
                       if not asset["id"].startswith(("humans/", "weapons/"))]
    updated_manifest = dict(current_manifest, version=max(2, current_manifest.get("version", 1)),
                            assets=retained_assets + bodies + weapons,
                            archived_assets=list(archive_by_id.values()))
    check_asset_inventory(updated_manifest)
    return updated_manifest


def publish_manifest():
    """Validate both delivered fragments before publishing the active input catalog."""
    source_root = CONCEPT_ROOT / "2026-10-05-modular-infantry"
    bodies = load_fragment(source_root / "bodies/manifest.json")
    weapons = load_fragment(source_root / "weapons/manifest.json")
    manifest_path = EXPORT_ROOT / "manifest.json"
    updated_manifest = build_manifest(json.loads(manifest_path.read_text()), bodies, weapons)
    for asset in check_asset_inventory(updated_manifest):
        check_asset_views(asset)
        for view in asset["views"]:
            check_view_file(asset, view, EXPORT_ROOT, CONCEPT_ROOT)
    manifest_path.write_text(json.dumps(updated_manifest, ensure_ascii=False, indent=2) + "\n")
    print("Published six shared bodies and eight weapons; 24 prior human variants preserved as inactive references")


if __name__ == "__main__":
    publish_manifest()
