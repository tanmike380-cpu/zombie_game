"""Package six warm headquarters concepts without changing models or game assets.

Image authoring is exclusively image_gen. This tool checks provenance, snapshots
references, publishes a review and updates only the six headquarters Tripo inputs.
"""

import argparse
import hashlib
import html
import json
import shutil
from pathlib import Path

from PIL import Image

from tools.art.concept_palette_review import resolve_within

PROJECT_ROOT = Path(__file__).resolve().parents[2]
CONCEPT_ROOT = PROJECT_ROOT / "art/concepts"
REVISION_ROOT = CONCEPT_ROOT / "2026-10-11-warm-headquarters"
CATALOG_PATH = CONCEPT_ROOT / "tripo/manifest.json"
VIEW_NAMES = {"front-perspective", "side", "rear-perspective"}
HQ_IDS = {f"buildings/{faction}/{age:02}-hq-age{age}"
          for faction in ("byzantine", "tianchao") for age in (1, 2, 3)}


def read_json(file_path):
    """Read an existing UTF-8 metadata file."""
    return json.loads(file_path.read_text(encoding="utf-8"))


def save_json(file_path, record):
    """Persist artifact metadata with stable readable formatting."""
    file_path.parent.mkdir(parents=True, exist_ok=True)
    file_path.write_text(json.dumps(record, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")


def hash_file(file_path):
    """Return a content digest for exact-copy and regression checks."""
    return hashlib.sha256(file_path.read_bytes()).hexdigest()


def copy_checked(source_path, target_path):
    """Copy a source exactly; never overwrite a differing preserved reference."""
    target_path.parent.mkdir(parents=True, exist_ok=True)
    if target_path.exists() and hash_file(target_path) != hash_file(source_path):
        raise ValueError(f"Preserved file already differs: {target_path}")
    if source_path.resolve() != target_path.resolve():
        shutil.copyfile(source_path, target_path)
    if hash_file(source_path) != hash_file(target_path):
        raise ValueError(f"Copy verification failed: {target_path}")


def load_assets():
    """Load only the two disjoint faction fragments."""
    assets = []
    for faction in ("byzantine", "tianchao"):
        assets.extend(read_json(REVISION_ROOT / faction / "fragment.json")["assets"])
    return sorted(assets, key=lambda asset: asset["id"])


def validate_assets(assets):
    """Require six unique 5×5 HQs and eighteen traceable independent images."""
    if len(assets) != 6 or {asset["id"] for asset in assets} != HQ_IDS:
        raise ValueError("Expected exactly the six faction/age headquarters")
    output_paths = []
    for asset in assets:
        if asset.get("footprint_tiles") != [5, 5]:
            raise ValueError(f"HQ must retain provisional 5×5 plot: {asset['id']}")
        if not asset.get("prompt") or not asset.get("visual_review"):
            raise ValueError(f"Missing generation/review provenance: {asset['id']}")
        if len(asset["views"]) != 3 or {view["name"] for view in asset["views"]} != VIEW_NAMES:
            raise ValueError(f"Missing independent angle: {asset['id']}")
        for view in asset["views"]:
            source_path = resolve_within(PROJECT_ROOT, view["source"])
            output_path = resolve_within(REVISION_ROOT, view["path"])
            if source_path == output_path:
                raise ValueError("New image must not overwrite its reference")
            if hash_file(source_path) != view["source_sha256"] or hash_file(output_path) != view["sha256"]:
                raise ValueError(f"Image hash mismatch: {asset['id']}/{view['name']}")
            with Image.open(output_path) as image:
                image.load()
                if image.size != (view["width"], view["height"]) or min(image.size) < 150:
                    raise ValueError(f"Invalid native image dimensions: {output_path}")
            output_paths.append(view["path"])
    if len(output_paths) != len(set(output_paths)):
        raise ValueError("Different angles must use separate files")


def freeze_references(assets):
    """Preserve all per-view inputs before retiring cool drafts or replacing HQs."""
    for asset in assets:
        faction, age_name = asset["id"].split("/")[1:]
        for view in asset["views"]:
            source_path = resolve_within(PROJECT_ROOT, view["source"])
            target_path = REVISION_ROOT / "reference-inputs" / faction / age_name / (view["name"] + ".png")
            copy_checked(source_path, target_path)
            view.setdefault("original_source", view["source"])
            view["source"] = target_path.relative_to(PROJECT_ROOT).as_posix()
        for palette_source in asset.get("palette_sources", []):
            source_path = resolve_within(PROJECT_ROOT, palette_source["path"])
            if hash_file(source_path) != palette_source["sha256"]:
                raise ValueError(f"Palette reference changed: {source_path}")
            if source_path.is_relative_to(REVISION_ROOT / "reference-inputs"):
                continue
            target_path = REVISION_ROOT / "reference-inputs" / faction / age_name / ("palette-" + source_path.name)
            copy_checked(source_path, target_path)
            palette_source.setdefault("original_path", palette_source["path"])
            palette_source["path"] = target_path.relative_to(PROJECT_ROOT).as_posix()
        freeze_generation_inputs(asset)
    for faction in ("byzantine", "tianchao"):
        fragment_path = REVISION_ROOT / faction / "fragment.json"
        fragment = read_json(fragment_path)
        fragment["assets"] = [asset for asset in assets if f"/{faction}/" in asset["id"]]
        fragment["frozen_reference_note"] = (
            "views.source及palette_sources.path现指向reference-inputs中的逐字节冻结输入；"
            "original_source/original_path保留生成时位置。冷稿停用或当前Tripo更新不影响这些输入。")
        save_json(fragment_path, fragment)


def freeze_generation_inputs(record):
    """Recursively preserve actual inputs of additional angle/material corrections."""
    if isinstance(record, list):
        for child in record:
            freeze_generation_inputs(child)
    elif isinstance(record, dict):
        if "source" in record and "source_sha256" in record:
            source_path = resolve_within(PROJECT_ROOT, record["source"])
            if hash_file(source_path) != record["source_sha256"]:
                raise ValueError(f"Generation input changed: {source_path}")
            if not source_path.is_relative_to(REVISION_ROOT / "reference-inputs"):
                target_path = REVISION_ROOT / "reference-inputs/generation" / (record["source_sha256"] + source_path.suffix)
                copy_checked(source_path, target_path)
                record.setdefault("original_source", record["source"])
                record["source"] = target_path.relative_to(PROJECT_ROOT).as_posix()
        for child in record.values():
            if isinstance(child, (dict, list)):
                freeze_generation_inputs(child)


def build_asset_card(asset):
    """Show the front and link separate native side/rear images for Tripo."""
    faction = "天朝" if "/tianchao/" in asset["id"] else "拜占庭"
    age = asset["id"][-1]
    front_view = next(view for view in asset["views"] if view["name"] == "front-perspective")
    labels = {"front-perspective": "前斜视", "side": "侧面", "rear-perspective": "后斜视"}
    links = " · ".join(f'<a href="{html.escape(view["path"], quote=True)}">{labels[view["name"]]} '
                       f'({view["width"]}×{view["height"]})</a>' for view in asset["views"])
    return (f'<article><h2>{faction} · 时代{age}</h2>'
            f'<img src="{html.escape(front_view["path"], quote=True)}" alt="{faction}时代{age}">'
            f'<p>{links}</p><small>5×5概念占地，尚未修改模型或游戏占地</small></article>')


def publish_review(assets):
    """Save the current palette decision and the six concept review cards."""
    manifest = {"version": 1, "date": "2026-10-11", "status": "awaiting-user-review",
                "human_palette": "original-warm", "footprint_provisional": True,
                "runtime_assets_changed": False, "assets": assets}
    save_json(REVISION_ROOT / "manifest.json", manifest)
    cards = "\n".join(build_asset_card(asset) for asset in assets)
    review_html = """<!doctype html><html lang="zh-CN"><meta charset="utf-8">
<meta name="viewport" content="width=device-width,initial-scale=1"><title>暖色主基地 · 三时代</title>
<style>body{background:#20242a;color:#eee;font:16px/1.6 system-ui;margin:0}main{max-width:1500px;margin:auto;padding:28px}
.cards{display:grid;grid-template-columns:repeat(3,1fr);gap:18px}article{background:#30353c;padding:16px;border-radius:10px}
h1{font-size:26px}h2{font-size:20px}img{width:100%;height:390px;object-fit:contain;background:#eee}a{color:#efc79c}
small{color:#bec3ca}@media(max-width:900px){.cards{grid-template-columns:1fr}img{height:360px}}</style>
<main><h1>暖色人类 · 两国主基地</h1><p>普通建筑和士兵保留原暖色稿，窗灯不改。
时代一、二延续已认可布局并恢复暖材质；时代三提升中央建筑高度与气势。天朝以高级木构为主，石材限于基座、台阶与铺地。
三时代暂定相同5×5占地，不整体拉大门窗或小房子。日光、Blender和Unity本轮未改。</p>
<p>每个主基地三个独立图片；给Tripo使用角度链接中的PNG，不上传本页拼排。二维AI概念并非精确建筑测绘，跨视角细节仍需建模校正。</p>
<div class="cards">""" + cards + "</div></main></html>\n"
    (REVISION_ROOT / "review.html").write_text(review_html, encoding="utf-8")


def activate_headquarters(assets):
    """Replace only six concept inputs, preserving all previous HQ PNGs/metadata."""
    catalog = read_json(CATALOG_PATH)
    backup_path = REVISION_ROOT / "previous-warm/catalog-before.json"
    if not backup_path.exists():
        save_json(backup_path, catalog)
    previous_catalog = read_json(backup_path)
    replacements = {asset["id"]: asset for asset in assets}
    for old_asset in previous_catalog["assets"]:
        if old_asset["id"] in HQ_IDS:
            for view in old_asset["views"]:
                old_path = CONCEPT_ROOT / "tripo" / old_asset["id"] / (view["name"] + ".png")
                backup_file = REVISION_ROOT / "previous-warm" / old_asset["id"] / old_path.name
                if not backup_file.exists():
                    copy_checked(old_path, backup_file)
                if hash_file(backup_file) != view["sha256"]:
                    raise ValueError(f"Previous HQ was not safely preserved: {backup_file}")
    for catalog_asset in catalog["assets"]:
        if catalog_asset["id"] not in replacements:
            continue
        replacement = replacements[catalog_asset["id"]]
        new_views = []
        for view in replacement["views"]:
            image_path = REVISION_ROOT / view["path"]
            output_path = CONCEPT_ROOT / "tripo" / catalog_asset["id"] / (view["name"] + ".png")
            shutil.copyfile(image_path, output_path)
            new_views.append({key: view[key] for key in ("name", "width", "height", "sha256")}
                             | {"source": image_path.relative_to(CONCEPT_ROOT).as_posix(), "copy_source": True})
        catalog_asset.update(views=new_views, footprint_tiles=[5, 5], footprint_provisional=True,
                             concept_revision="2026-10-11-warm-headquarters")
    validate_unaffected_catalog(previous_catalog, catalog)
    save_json(CATALOG_PATH, catalog)


def validate_unaffected_catalog(previous_catalog, catalog):
    """Reject changes to ordinary buildings, people, weapons, zombies or archives."""
    previous_assets = {asset["id"]: asset for asset in previous_catalog["assets"]}
    if set(previous_assets) != {asset["id"] for asset in catalog["assets"]}:
        raise ValueError("Catalog roster changed outside the HQ task")
    if previous_catalog.get("archived_assets") != catalog.get("archived_assets"):
        raise ValueError("Historical concept inventory changed")
    for asset in catalog["assets"]:
        if asset["id"] in HQ_IDS:
            continue
        if previous_assets[asset["id"]] != asset:
            raise ValueError(f"Unrelated concept metadata changed: {asset['id']}")
        for view in asset["views"]:
            image_path = CONCEPT_ROOT / "tripo" / asset["id"] / (view["name"] + ".png")
            if hash_file(image_path) != view["sha256"]:
                raise ValueError(f"Unrelated concept image changed: {image_path}")


def main():
    """Run explicit packaging, validation or narrowly scoped catalog activation."""
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("command", choices=("validate", "publish", "activate"))
    args = parser.parse_args()
    assets = load_assets()
    validate_assets(assets)
    if args.command == "publish":
        freeze_references(assets)
        publish_review(assets)
    elif args.command == "activate":
        activate_headquarters(assets)
    print("PASS: six headquarters, eighteen independent PNGs; no runtime asset changes")


if __name__ == "__main__":
    main()
