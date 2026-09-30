"""Register the reviewed generated turnarounds and their exact panel cuts."""

import json
from PIL import Image
from tools.art.concept_views import CONCEPT_ROOT, EXPORT_ROOT

# Unequal column boundaries measured from the actual generated images.
PANEL_EDGES = {
    "humans/tianchao/repeater/veteran": [0, 565, 1024, 1536],
    "humans/tianchao/three-eyed/militia": [0, 540, 1024, 1536],
    "humans/byzantine/crossbowman/veteran": [0, 558, 1024, 1536],
    "siege/tianchao/ballista": [0, 490, 1046, 1536],
    "siege/tianchao/trebuchet": [0, 477, 1078, 1536],
    "siege/tianchao/nest-of-bees": [0, 561, 1213, 1774],
    "zombies/03-hound": [0, 469, 1413, 1881],
    "zombies/07-carrier": [0, 510, 1075, 1536],
}


def register_generated_views():
    """Add completed image_gen assets without changing any authored balance."""
    manifest_path = EXPORT_ROOT / "manifest.json"
    manifest = json.loads(manifest_path.read_text())
    records = json.loads((CONCEPT_ROOT / "2026-09-30-turnarounds/provenance.json").read_text())
    generated_ids = {record["id"] for record in records}
    manifest["assets"] = [asset for asset in manifest["assets"] if asset["id"] not in generated_ids]
    for record in records:
        with Image.open(CONCEPT_ROOT / record["source"]) as source:
            width, height = source.size
        edges = PANEL_EDGES.get(record["id"], [0, width // 3, width * 2 // 3, width])
        views = [{"name": name, "source": record["source"],
                  "box": [edges[index] + 2, 0, edges[index + 1] - 2, height]}
                 for index, name in enumerate(("front", "side", "back"))]
        manifest["assets"].append({"id": record["id"], "label": record["id"],
                                   "category": record["id"].split("/")[0].rstrip("s"),
                                   "projection": "AI reference views; not a calibrated orthographic reconstruction",
                                   "views": views})
    manifest_path.write_text(json.dumps(manifest, ensure_ascii=False, indent=2) + "\n")
    print(f"Registered {len(records)} generated turnarounds; total {len(manifest['assets'])} assets")


if __name__ == "__main__":
    register_generated_views()
