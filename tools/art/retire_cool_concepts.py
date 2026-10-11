"""Recoverably retire the user-rejected cool concepts, not unrelated artwork.

The exact dated folder is moved to a project-local ignored backup. Its inputs and
approved Western common-zombie alternative are preserved before the move.
"""

import shutil

from tools.art import headquarters_review as review

COLD_ROOT = review.CONCEPT_ROOT / "2026-10-11-cool-revision"
BACKUP_ROOT = review.PROJECT_ROOT / "Builds/ArtReview/ConceptBackups/2026-10-11-cool-revision"
WESTERN_ROOT = review.CONCEPT_ROOT / "2026-10-11-western-common"


def inventory_files(root_path):
    """Record exact file digests, rejecting links that could escape the art folder."""
    inventory = {}
    for file_path in sorted(root_path.rglob("*")):
        if file_path.is_symlink():
            raise ValueError(f"Refusing to archive a symlink: {file_path}")
        if file_path.is_file():
            inventory[file_path.relative_to(root_path).as_posix()] = review.hash_file(file_path)
    return inventory


def preserve_western_common():
    """Keep the separately approved costume change outside rejected palette drafts."""
    fragment = review.read_json(COLD_ROOT / "units/fragment-common.json")
    asset = fragment["assets"][0]
    source_dir = COLD_ROOT / "units/zombies/01-common-western-safe"
    for source_path in source_dir.iterdir():
        if source_path.is_file():
            review.copy_checked(source_path, WESTERN_ROOT / source_path.name)
    asset["sheet"] = "generated-sheet.png"
    for view in asset["views"]:
        view["path"] = view["name"] + ".png"
    review.save_json(WESTERN_ROOT / "manifest.json", {
        "status": "retained-costume-alternative-not-runtime-activated",
        "reason": "User-approved Western old-clothing alternative, not a human palette revision",
        "assets": [asset], "runtime_assets_changed": False})


def snapshot_source_images():
    """Freeze the historical revision inputs so later HQ changes cannot erase them."""
    manifest = review.read_json(COLD_ROOT / "manifest.json")
    snapshots = {}
    for asset in manifest["assets"]:
        for view in asset["views"]:
            source_path = review.resolve_within(review.PROJECT_ROOT, view["source"])
            if review.hash_file(source_path) != view["source_sha256"]:
                raise ValueError(f"Historical reference already changed: {source_path}")
            snapshot_path = BACKUP_ROOT / "source-snapshots" / (view["source_sha256"] + source_path.suffix)
            review.copy_checked(source_path, snapshot_path)
            snapshots[view["source"]] = snapshot_path.relative_to(BACKUP_ROOT).as_posix()
    return snapshots


def retire_revision():
    """Move exactly the superseded dated directory and leave a discoverable notice."""
    review.validate_assets(review.load_assets())
    if not (review.REVISION_ROOT / "manifest.json").is_file():
        raise ValueError("Publish checked warm HQs before retiring their cool references")
    if BACKUP_ROOT.exists():
        raise ValueError(f"Backup exists; refusing to overwrite it: {BACKUP_ROOT}")
    if not COLD_ROOT.is_dir() or (COLD_ROOT / "retired.json").exists():
        raise ValueError("Cool revision is missing or already retired")
    inventory = inventory_files(COLD_ROOT)
    preserve_western_common()
    snapshots = snapshot_source_images()
    archive_path = BACKUP_ROOT / "original-folder"
    shutil.move(str(COLD_ROOT), str(archive_path))
    if inventory_files(archive_path) != inventory:
        raise ValueError("Archive verification failed; backup retained, do not delete it")
    retirement = {"status": "retired-by-user-request", "date": "2026-10-11",
                  "backup": BACKUP_ROOT.relative_to(review.PROJECT_ROOT).as_posix(),
                  "archived_files": len(inventory), "archive_sha256": inventory,
                  "source_snapshots": snapshots, "restoration_commit": "67b013e",
                  "replacement": "art/concepts/2026-10-11-warm-headquarters/review.html"}
    review.save_json(BACKUP_ROOT / "retirement.json", retirement)
    review.save_json(COLD_ROOT / "retired.json", {key: value for key, value in retirement.items()
                                                 if key not in ("archive_sha256", "source_snapshots")})
    (COLD_ROOT / "README.md").write_text(
        "# 已停用：整批冷色修订\n\n2026-10-11按用户要求从当前概念目录移除。"
        "普通人类建筑与士兵沿用原暖色稿；主基地见[暖色三时代新版](../2026-10-11-warm-headquarters/README.md)。\n\n"
        "原文件未永久销毁，完整本地备份位于 `Builds/ArtReview/ConceptBackups/2026-10-11-cool-revision/original-folder/`。"
        "已跟踪内容亦可从Git提交 `67b013e` 取回，不要重置整个工作区。"
        "[普通僵尸西方换装候选](../2026-10-11-western-common/README.md)独立保留。\n", encoding="utf-8")
    (COLD_ROOT / "review.html").write_text(
        '<!doctype html><html lang="zh-CN"><meta charset="utf-8"><title>冷色修订已停用</title>'
        '<main><h1>冷色修订已停用</h1><p>人类保留原暖色稿，旧修订已移至可恢复备份。</p>'
        '<a href="../2026-10-11-warm-headquarters/review.html">查看暖色主基地三时代新版</a></main></html>\n',
        encoding="utf-8")
    print(f"Retired {len(inventory)} files to verified recoverable backup; no permanent deletion")


if __name__ == "__main__":
    retire_revision()
