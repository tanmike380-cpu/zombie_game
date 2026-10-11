"""Validate non-destructive palette revisions and build their local review index.

This module never changes image pixels or activates draft concepts in the game.
Image authoring belongs to image_gen; only traceability and presentation live here.
"""

import argparse
import hashlib
import html
import json
from pathlib import Path

from PIL import Image

PROJECT_ROOT = Path(__file__).resolve().parents[2]
CONCEPT_ROOT = PROJECT_ROOT / "art/concepts"
REVISION_ROOT = CONCEPT_ROOT / "2026-10-11-cool-revision"
FRAGMENT_PATHS = ("buildings/fragment.json", "units/fragment.json",
                  "zombie-lairs/ownfragment.json", "resources/ownfragment.json",
                  "buildings/fragment-tianchao.json", "buildings/fragment-units-buildings.json",
                  "units/fragment-common.json")


def load_expected_views():
    """Return prior asset IDs and their independently authored source view names."""
    catalog = json.loads((CONCEPT_ROOT / "tripo/manifest.json").read_text())
    expected_views = {}
    for asset in catalog["assets"]:
        asset_id = asset["id"]
        is_selected = (
            asset_id.startswith("buildings/byzantine/")
            or asset_id in {f"buildings/tianchao/{number}-hq-age{age}"
                            for number, age in (("01", 1), ("02", 2), ("03", 3))}
            or (asset_id.startswith("humans/") and asset_id.endswith("/militia"))
            or asset_id in {"zombies/01-common", "zombies/06-headram", "zombies/07-carrier"}
            or asset_id == "weapons/tianchao/repeating-crossbow-views-v2"
            or asset_id == "siege/tianchao/trebuchet"
        )
        if is_selected:
            expected_views[asset_id] = {view["name"] for view in asset["views"]}
    lairs = json.loads((CONCEPT_ROOT / "2026-10-11-zombie-lairs/manifest.json").read_text())
    for asset in lairs["assets"]:
        if asset["culture"] == "middleeast" and any(
            f"/{number}-" in asset["id"] for number in ("11", "12", "13", "15")
        ):
            expected_views[asset["id"]] = {"reference"}
    for mineral in ("stone", "iron", "gold"):
        expected_views[f"resources/01-natural/{mineral}"] = {"front", "side", "back"}
    return expected_views


def load_fragments():
    """Combine disjoint specialist fragments without silently dropping duplicates."""
    assets = []
    blocked = []
    fragment_paths = [REVISION_ROOT / fragment for fragment in FRAGMENT_PATHS]
    for fragment_path in fragment_paths:
        if not fragment_path.is_file():
            raise FileNotFoundError(f"Missing revision fragment: {fragment_path}")
        fragment = json.loads(fragment_path.read_text())
        assets.extend(fragment["assets"])
        blocked.extend(fragment.get("blocked", []))
    asset_ids = [asset["id"] for asset in assets]
    if len(set(asset_ids)) != len(asset_ids):
        raise ValueError("Duplicate IDs in palette revision fragments")
    assets, blocked = reconcile_approved_alternatives(assets, blocked)
    return sorted(assets, key=lambda asset: asset["id"]), blocked


def reconcile_approved_alternatives(assets, blocked):
    """Retain failed-attempt history only for explicitly approved completed alternatives."""
    alternatives = {asset["id"]: asset for asset in assets
                    if asset.get("replaces_blocked_attempt")
                    and asset.get("user_approved_alternative")}
    remaining_blocked = []
    for failed_attempt in blocked:
        alternative = alternatives.get(failed_attempt["id"])
        if alternative is None:
            remaining_blocked.append(failed_attempt)
        else:
            alternative.setdefault("prior_failed_attempts", []).append(failed_attempt)
    return assets, remaining_blocked


def resolve_within(root_path, relative_path):
    """Resolve an existing file while rejecting absolute and escaping paths."""
    if Path(relative_path).is_absolute():
        raise ValueError(f"Expected a relative path: {relative_path}")
    file_path = (root_path / relative_path).resolve()
    if not file_path.is_relative_to(root_path.resolve()) or not file_path.is_file():
        raise ValueError(f"Missing or out-of-root file: {relative_path}")
    return file_path


def validate_view(view):
    """Check output dimensions, recorded hashes and preservation of the old image."""
    source_path = resolve_within(PROJECT_ROOT, view["source"])
    output_path = resolve_within(REVISION_ROOT, view["path"])
    if source_path == output_path:
        raise ValueError(f"Revision overwrites its source: {output_path}")
    for file_path, expected_hash in (
        (source_path, view["source_sha256"]), (output_path, view["sha256"])
    ):
        if hashlib.sha256(file_path.read_bytes()).hexdigest() != expected_hash:
            raise ValueError(f"Image hash mismatch: {file_path}")
    with Image.open(output_path) as output_image:
        output_image.load()
        if output_image.size != (view["width"], view["height"]):
            raise ValueError(f"Image size mismatch: {output_path}")
        if min(output_image.size) < 150:
            raise ValueError(f"Image panel too small: {output_path}")


def validate_assets(assets, blocked):
    """Require the whole scoped revision, prompts and complete independent angles."""
    expected_views = load_expected_views()
    asset_ids = {asset["id"] for asset in assets}
    blocked_ids = {item["id"] for item in blocked}
    if len(blocked_ids) != len(blocked) or blocked_ids & asset_ids:
        raise ValueError("Blocked asset records are duplicated or also reported as completed")
    for item in blocked:
        if not item.get("reason") or item.get("status") != "blocked-no-output":
            raise ValueError(f"Incomplete blocked asset disclosure: {item}")
    if asset_ids | blocked_ids != set(expected_views):
        missing_ids = set(expected_views) - (asset_ids | blocked_ids)
        extra_ids = (asset_ids | blocked_ids) - set(expected_views)
        raise ValueError(f"Revision roster mismatch: missing={missing_ids}, extra={extra_ids}")
    output_paths = []
    for asset in assets:
        names = [view["name"] for view in asset["views"]]
        if set(names) != expected_views[asset["id"]] or len(names) != len(set(names)):
            raise ValueError(f"Incomplete or duplicated views: {asset['id']}")
        if not asset.get("prompt") or not asset.get("visual_review"):
            raise ValueError(f"Missing prompt or visual review: {asset['id']}")
        if "-hq-age" in asset["id"] and asset.get("footprint_tiles") != [5, 5]:
            raise ValueError(f"HQ concept must share the provisional 5×5 footprint: {asset['id']}")
        for view in asset["views"]:
            validate_view(view)
            output_paths.append(view["path"])
    if len(set(output_paths)) != len(output_paths):
        raise ValueError("Different revision views share an output file")


def build_asset_card(asset):
    """Render one before/after pair with links to every independent angle."""
    main_view = next((view for view in asset["views"] if view["name"].startswith("front")),
                     asset["views"][0])
    old_url = "../../../" + main_view["source"]
    view_labels = {"front": "正面", "side": "侧面", "back": "背面",
                   "front-perspective": "正前斜视", "rear-perspective": "后方斜视",
                   "reference": "独立概念图"}
    view_links = " · ".join(
        f'<a href="{html.escape(view["path"], quote=True)}">'
        f'{html.escape(view_labels.get(view["name"], view["name"]))} '
        f'({view["width"]}×{view["height"]})</a>' for view in asset["views"]
    )
    return (
        f'<article><h2>{html.escape(asset.get("label", asset["id"]))}</h2>'
        f'<small>{html.escape(asset["id"])}</small><div class="pair">'
        f'<figure><img loading="lazy" src="{html.escape(old_url, quote=True)}" alt="旧稿">'
        '<figcaption>旧稿</figcaption></figure>'
        f'<figure><img loading="lazy" src="{html.escape(main_view["path"], quote=True)}" alt="冷色修订">'
        f'<figcaption>冷色修订</figcaption></figure></div><p>{view_links}</p></article>'
    )


def build_review_html(assets, blocked):
    """Build a lightweight, local-only comparison page without modifying any art."""
    headquarters = [asset for asset in assets if "-hq-age" in asset["id"]]
    other_assets = [asset for asset in assets if "-hq-age" not in asset["id"]]
    hq_cards = "\n".join(build_asset_card(asset) for asset in headquarters)
    other_cards = "\n".join(build_asset_card(asset) for asset in other_assets)
    warnings = "".join(f'<p>未完成：{html.escape(item["id"])} — {html.escape(item["reason"])}</p>'
                       for item in blocked)
    return """<!doctype html><html lang="zh-CN"><meta charset="utf-8">
<meta name="viewport" content="width=device-width,initial-scale=1">
<title>冷色基底概念图修订</title><style>
body{margin:0;background:#171e27;color:#e4eaf0;font:16px/1.6 system-ui,sans-serif}
main{max-width:1440px;margin:auto;padding:28px}h1{font-size:27px}h2{font-size:19px}
article{margin:26px 0;padding:20px;background:#25303c;border-radius:10px}
small{color:#aab9ca}.pair{display:grid;grid-template-columns:1fr 1fr;gap:16px}
figure{margin:10px 0}img{display:block;width:100%;height:440px;object-fit:contain;background:#eee}
figcaption{text-align:center}a{color:#9dc4f2}@media(max-width:700px){img{height:260px}main{padding:12px}}
</style><main><h1>冷色基底概念图修订</h1>
<p>冷灰阴影为基底，人类保留微暖的米灰石材、自然肤色、木材与皮革；不统一漂白。
僵尸与感染建筑允许更冷。原概念图全部原址保留。</p>
<p>两国主基地三个时代暂定同为5×5格，以木构到石构、稀疏到密集展示升级。
建筑门窗尺度保持一致，不把小村庄整体拉大。拜占庭主基地取消金属结构与金属装饰。</p>
<p>这里是二维评审稿，不会自动替换 Tripo 目录、Blender 模型或 Unity 实机资产。
点击角度链接查看独立图片，给 Tripo 使用时不要上传评审拼图。</p>
""" + warnings + '<h2>主基地三个时代</h2>' + hq_cards + '<h2>其他冷调修订</h2>' + other_cards + "</main></html>\n"


def save_review(assets, blocked):
    """Save the checked catalog and its index beside the non-destructive revisions."""
    previous_catalog = json.loads((CONCEPT_ROOT / "tripo/manifest.json").read_text())
    labels = {asset["id"]: asset.get("label", asset["id"]) for asset in previous_catalog["assets"]}
    for asset in assets:
        if asset.get("label", asset["id"]) == asset["id"]:
            asset["label"] = labels.get(asset["id"], asset["id"])
        id_parts = asset["id"].split("/")
        faction = id_parts[1] if len(id_parts) > 1 else ""
        if faction in ("tianchao", "byzantine") and not any(
            text in asset["label"] for text in ("天朝", "拜占庭")
        ):
            asset["label"] = {"tianchao": "天朝", "byzantine": "拜占庭"}[faction] + " · " + asset["label"]
    manifest = {"version": 1, "date": "2026-10-11", "status": "partial-awaiting-user-review" if blocked else "awaiting-user-review",
                "tool_mode": "built-in-image_gen", "old_images_preserved": True,
                "runtime_assets_changed": False, "assets": assets, "blocked": blocked}
    (REVISION_ROOT / "manifest.json").write_text(json.dumps(manifest, ensure_ascii=False, indent=2) + "\n")
    (REVISION_ROOT / "review.html").write_text(build_review_html(assets, blocked))


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("command", choices=("validate", "publish"))
    args = parser.parse_args()
    assets, blocked = load_fragments()
    validate_assets(assets, blocked)
    if args.command == "publish":
        save_review(assets, blocked)
    print(f"PASS: {len(assets)} revised assets, {sum(len(asset['views']) for asset in assets)} independent PNGs; {len(blocked)} explicitly blocked")


if __name__ == "__main__":
    main()
