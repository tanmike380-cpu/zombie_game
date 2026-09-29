"""Reproducibly split approved concept sheets into single-view Tripo inputs.

Only performs the user-requested deterministic cropping/padding. Missing angles
are generated separately with image_gen, never fabricated by this exporter.
"""

import argparse
import hashlib
import json
from pathlib import Path

from PIL import Image, ImageDraw, ImageOps

PROJECT_ROOT = Path(__file__).resolve().parents[2]
CONCEPT_ROOT = PROJECT_ROOT / "art/concepts"
EXPORT_ROOT = CONCEPT_ROOT / "tripo"
REVIEW_ROOT = PROJECT_ROOT / "Builds/ArtReview/TripoViews"


def make_contact_sheet(image_paths, output_path, columns=4, cell_size=(384, 280)):
    """Make a labeled review-only contact sheet without changing source files."""
    rows = (len(image_paths) + columns - 1) // columns
    sheet = Image.new("RGB", (columns * cell_size[0], rows * cell_size[1]), "#eeeeee")
    draw = ImageDraw.Draw(sheet)
    for index, path in enumerate(image_paths):
        with Image.open(path) as source:
            thumbnail = ImageOps.contain(source.convert("RGB"), (cell_size[0], cell_size[1] - 30))
        x = (index % columns) * cell_size[0]
        y = (index // columns) * cell_size[1]
        sheet.paste(thumbnail, (x, y))
        draw.text((x + 5, y + cell_size[1] - 25), path.parent.name + "/" + path.name, fill="black")
    output_path.parent.mkdir(parents=True, exist_ok=True)
    sheet.save(output_path)


def export_crop(source_path, output_path, box, exclusions=None, polygon=None, erase=None):
    """Crop a reviewed pixel rectangle, optionally blanking neighboring panels.

    Exclusions are polygons in source pixel coordinates and must only cover
    background/text/other views, never the selected subject.
    """
    with Image.open(source_path) as source:
        source = source.convert("RGB")
        left, top, right, bottom = box
        if not (0 <= left < right <= source.width and 0 <= top < bottom <= source.height):
            raise ValueError(f"Invalid crop {box} for {source_path}: {source.size}")
        if exclusions:
            draw = ImageDraw.Draw(source)
            for exclusion in exclusions:
                draw.polygon(exclusion["points"], fill=tuple(exclusion["color"]))
        if erase:
            draw = ImageDraw.Draw(source)
            for rectangle in erase:
                draw.rectangle(rectangle, fill=source.getpixel((source.width - 1, source.height - 1)))
        if polygon:
            mask = Image.new("L", source.size, 0)
            ImageDraw.Draw(mask).polygon([tuple(point) for point in polygon], fill=255)
            background = Image.new("RGB", source.size, source.getpixel((source.width - 1, source.height - 1)))
            source = Image.composite(source, background, mask)
        crop = source.crop(box)
        # Padding only: do not upscale small source views or distort proportions.
        margin = 20
        background = crop.getpixel((0, 0))
        output = ImageOps.expand(crop, border=margin, fill=background)
    output_path.parent.mkdir(parents=True, exist_ok=True)
    output.save(output_path, optimize=True)
    return {"width": output.width, "height": output.height,
            "sha256": hashlib.sha256(output_path.read_bytes()).hexdigest()}


def export_manifest(manifest_path):
    """Export only explicitly reviewed source/view rectangles in a manifest."""
    manifest = json.loads(manifest_path.read_text())
    count = 0
    for asset in manifest["assets"]:
        for view in asset["views"]:
            source_path = CONCEPT_ROOT / view["source"]
            output_path = EXPORT_ROOT / asset["id"] / (view["name"] + ".png")
            metadata = export_crop(source_path, output_path, view["box"], view.get("exclusions"), view.get("polygon"), view.get("erase"))
            view.update(metadata)
            count += 1
    manifest_path.write_text(json.dumps(manifest, ensure_ascii=False, indent=2) + "\n")
    print(f"Exported {count} single-view images across {len(manifest['assets'])} assets")


def review_sources():
    """Review all current building and elite source sheets in small batches."""
    paths = []
    for faction in ("tianchao", "byzantine"):
        for path in sorted((CONCEPT_ROOT / "2026-09-28-buildings" / faction).glob("*.png")):
            if path.name == "15-castle.png":
                continue
            if path.with_stem(path.stem + "-v2").exists():
                continue
            paths.append(path)
    paths.extend(sorted((CONCEPT_ROOT / "2026-09-28-factions").glob("*.png")))
    for index in range(0, len(paths), 12):
        make_contact_sheet(paths[index:index + 12], REVIEW_ROOT / f"sources-{index // 12:02}.jpg")
    print(f"Review sheets for {len(paths)} sources saved to {REVIEW_ROOT}")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("command", choices=("review", "export"))
    args = parser.parse_args()
    if args.command == "review":
        review_sources()
    else:
        export_manifest(EXPORT_ROOT / "manifest.json")


if __name__ == "__main__":
    main()
