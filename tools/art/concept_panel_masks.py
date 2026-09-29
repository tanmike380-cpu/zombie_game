"""Find separate building illustration panels for the requested lossless cuts.

Thresholding locates panels; convex masks keep original colored pixels inside
each panel rather than attempting to repaint or invent building geometry.
"""

import json

import numpy as np
from PIL import Image, ImageDraw, ImageFilter
from scipy import ndimage
from scipy.spatial import ConvexHull

from tools.art.concept_views import CONCEPT_ROOT, EXPORT_ROOT


def find_components(source, threshold=143):
    """Return substantial connected illustration components at half resolution."""
    thumbnail = source.resize((source.width // 2, source.height // 2))
    pixels = np.asarray(thumbnail)
    dark_pixels = pixels.min(axis=2) < threshold
    # Close small gaps in light stonework without bridging separate panels.
    dark_pixels = ndimage.binary_closing(dark_pixels, iterations=3)
    dark_pixels = ndimage.binary_dilation(dark_pixels, iterations=2)
    labels, count = ndimage.label(dark_pixels)
    components = []
    areas = np.bincount(labels.ravel())
    for label, region in enumerate(ndimage.find_objects(labels), start=1):
        if areas[label] < 350:
            continue
        y, x = np.nonzero(labels[region] == label)
        y += region[0].start
        x += region[1].start
        components.append({"points": np.column_stack((x, y)) * 2,
                           "area": len(x), "center": (float(x.mean()) * 2, float(y.mean()) * 2)})
    return components


def build_hull_mask(source, component):
    """Enclose the panel including small highlights and roof ornaments."""
    points = component["points"]
    hull = points[ConvexHull(points).vertices]
    mask = Image.new("L", source.size, 0)
    ImageDraw.Draw(mask).polygon([tuple(point) for point in hull], fill=255)
    return mask.filter(ImageFilter.MaxFilter(17))


def select_component(components, width, height, view_name):
    """Select a panel by layout zone and component size, failing if absent."""
    if view_name == "front-perspective":
        matches = [item for item in components if item["center"][0] < width * .64]
    elif view_name == "rear-perspective":
        matches = [item for item in components if item["center"][0] > width * .62 and item["center"][1] < height * .55]
    else:
        matches = [item for item in components if item["center"][0] > width * .62 and item["center"][1] > height * .55]
    if not matches:
        raise ValueError(f"No illustration component found for {view_name}")
    return max(matches, key=lambda item: item["area"])


def update_building_masks():
    """Write explicit crop polygons to the manifest for repeatable export."""
    manifest_path = EXPORT_ROOT / "manifest.json"
    manifest = json.loads(manifest_path.read_text())
    for asset in manifest["assets"]:
        if asset["category"] != "building":
            continue
        with Image.open(CONCEPT_ROOT / asset["views"][0]["source"]) as source:
            source = source.convert("RGB")
            components = find_components(source, 125 if asset["id"] == "buildings/byzantine/14-stone-house" else 143)
            for view in asset["views"]:
                try:
                    component = select_component(components, source.width, source.height, view["name"])
                except ValueError:
                    print(asset["id"], view["name"], "KEEP MANUAL MASK: review required", flush=True)
                    continue
                mask = build_hull_mask(source, component)
                # Hull the dilated outline to serialize a compact mask, not a cache.
                outline = np.asarray(mask) > 0
                y, x = np.nonzero(outline & ~ndimage.binary_erosion(outline))
                points = np.column_stack((x, y))
                view["polygon"] = points[ConvexHull(points).vertices].tolist()
                view["box"] = list(mask.getbbox())
                view.pop("erase", None)
        print(asset["id"], "panels located", flush=True)
    manifest_path.write_text(json.dumps(manifest, ensure_ascii=False, indent=2) + "\n")


if __name__ == "__main__":
    update_building_masks()
