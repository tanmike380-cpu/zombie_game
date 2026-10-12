"""Download pinned CC0 Poly Haven source vegetation and verify each payload."""

import concurrent.futures
import hashlib
import http.client
import json
import logging
from pathlib import Path
import urllib.request
import urllib.error


PROJECT_ROOT = Path(__file__).resolve().parents[1]
SOURCE_ROOT = PROJECT_ROOT / "art/environment/polyhaven-src"
ASSET_IDS = ("grass_bermuda_01", "grass_medium_01", "shrub_02", "tree_small_02")
BIOME_TEXTURE_IDS = ("snow_02", "sand_01", "rocky_terrain_02", "forest_ground_04", "rock_boulder_dry",
                     "rock_face_03", "cliff_side", "gravelly_sand")
MAX_DOWNLOAD_ATTEMPTS = 3


def read_download_payload(url):
    """Retry incomplete network responses; never persist a partial asset."""
    for attempt in range(MAX_DOWNLOAD_ATTEMPTS):
        request = urllib.request.Request(url, headers={"User-Agent": "ZombieGame-AssetImport/1.0"})
        try:
            with urllib.request.urlopen(request, timeout=45) as response:
                return response.read()
        except (http.client.IncompleteRead, urllib.error.URLError, TimeoutError, ConnectionError) as error:
            if attempt + 1 == MAX_DOWNLOAD_ATTEMPTS:
                raise RuntimeError(f"Asset download failed after {MAX_DOWNLOAD_ATTEMPTS} attempts: {url}") from error
            logging.warning("Retrying interrupted asset download (%d/%d): %s", attempt + 1, MAX_DOWNLOAD_ATTEMPTS, url)


def get_json(url):
    """Read official public API metadata without storing credentials."""
    request = urllib.request.Request(url, headers={"User-Agent": "ZombieGame-AssetImport/1.0"})
    with urllib.request.urlopen(request, timeout=60) as response:
        return json.load(response)


def download_file(asset_id, relative_path, record):
    """Download or reuse a verified, precisely scoped source file."""
    destination = SOURCE_ROOT / asset_id / relative_path
    destination.parent.mkdir(parents=True, exist_ok=True)
    if destination.exists() and hashlib.md5(destination.read_bytes()).hexdigest() != record["md5"]:
        raise ValueError(f"Locally modified source preserved: {destination}")
    if not destination.exists():
        payload = read_download_payload(record["url"])
        if hashlib.md5(payload).hexdigest() != record["md5"]:
            raise ValueError(f"Source checksum mismatch: {asset_id}/{relative_path}")
        destination.write_bytes(payload)
    payload = destination.read_bytes()
    return {"path": str(destination.relative_to(PROJECT_ROOT)), "url": record["url"],
            "bytes": len(payload), "md5": record["md5"], "sha256": hashlib.sha256(payload).hexdigest()}


def download_asset(asset_id):
    """Preserve original Blender source plus its authored texture dependencies."""
    metadata = get_json(f"https://api.polyhaven.com/info/{asset_id}")
    record = get_json(f"https://api.polyhaven.com/files/{asset_id}")["blend"]["1k"]["blend"]
    files = [(f"{asset_id}_1k.blend", record), *record.get("include", {}).items()]
    # Unity uses conventional PNG/JPG maps, while Blender source retains original EXR dependencies.
    channels = get_json(f"https://api.polyhaven.com/files/{asset_id}")
    for channel_name, resolutions in channels.items():
        if channel_name in ("blend", "fbx", "gltf", "usd") or "1k" not in resolutions:
            continue
        formats = resolutions["1k"]
        entry = formats.get("png") or formats.get("jpg")
        if entry and "url" in entry:
            filename = "textures/" + entry["url"].split("/")[-1]
            if not any(item[0] == filename for item in files):
                files.append((filename, entry))
    with concurrent.futures.ThreadPoolExecutor(max_workers=4) as pool:
        downloaded = list(pool.map(lambda item: download_file(asset_id, *item), files))
    logging.info("Verified %s (%d files)", asset_id, len(downloaded))
    return {"id": asset_id, "title": metadata["name"], "source": f"https://polyhaven.com/a/{asset_id}",
            "authors": metadata.get("authors", {}), "license": "CC0-1.0",
            "license_url": "https://polyhaven.com/license", "texture_resolution": "1K", "files": downloaded}


def download_biome_textures():
    """Pin real 2K ground, cliff and scree channels for independent test biomes."""
    manifest_path = SOURCE_ROOT.parent / "showcase_biome_sources.json"
    if manifest_path.exists():
        pinned = json.loads(manifest_path.read_text(encoding="utf-8"))
        for asset in pinned["assets"]:
            for record in asset["files"]:
                download_file(asset["id"], "textures/" + Path(record["path"]).name, record)
        assets = pinned["assets"]
    else:
        assets = []
    for asset_id in BIOME_TEXTURE_IDS:
        if any(asset["id"] == asset_id for asset in assets):
            continue
        metadata = get_json(f"https://api.polyhaven.com/info/{asset_id}")
        channels = get_json(f"https://api.polyhaven.com/files/{asset_id}")
        files = []
        for channel in ("Diffuse", "nor_gl", "Rough"):
            formats = channels[channel]["2k"]
            record = formats.get("jpg") or formats["png"]
            file = download_file(asset_id, "textures/" + record["url"].split("/")[-1], record)
            file["channel"] = channel
            files.append(file)
        assets.append({"id": asset_id, "source": f"https://polyhaven.com/a/{asset_id}",
                       "authors": metadata.get("authors", {}), "license": "CC0-1.0",
                       "license_url": "https://polyhaven.com/license", "texture_resolution": "2K",
                       "files": files})
        logging.info("Verified biome material %s", asset_id)
    manifest_path.write_text(json.dumps({"schema_version": 1, "assets": assets}, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")


def main():
    """Write traceable, verified import manifest for the independent environment scene."""
    logging.basicConfig(level=logging.INFO, format="%(levelname)s %(message)s")
    manifest_path = SOURCE_ROOT.parent / "vegetation_source_manifest.json"
    if manifest_path.exists():
        pinned = json.loads(manifest_path.read_text(encoding="utf-8"))
        for asset in pinned["assets"]:
            for record in asset["files"]:
                relative_path = str(Path(record["path"]).relative_to(SOURCE_ROOT.relative_to(PROJECT_ROOT) / asset["id"]))
                download_file(asset["id"], relative_path, record)
        assets = pinned["assets"]
    else:
        assets = [download_asset(asset_id) for asset_id in ASSET_IDS]
    manifest = {"schema_version": 1, "verified_date": "2026-10-11", "provider": "Poly Haven",
                "attribution": "Powered by Poly Haven", "assets": assets}
    if not manifest_path.exists():
        manifest_path.write_text(json.dumps(manifest, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    ground_path = SOURCE_ROOT.parent / "showcase_ground_source.json"
    if ground_path.exists():
        pinned_ground = json.loads(ground_path.read_text(encoding="utf-8"))
        for record in pinned_ground["files"]:
            download_file("grass_ground", "textures/" + Path(record["path"]).name, record)
        download_biome_textures()
        return
    terrain_files = get_json("https://api.polyhaven.com/files/grass_ground")
    terrain_info = get_json("https://api.polyhaven.com/info/grass_ground")
    ground_files = []
    for channel in ("Diffuse", "nor_gl", "Rough"):
        record = terrain_files[channel]["2k"]["jpg"]
        ground_files.append(download_file("grass_ground", "textures/" + record["url"].split("/")[-1], record))
    ground_manifest = {"id": "grass_ground", "source": "https://polyhaven.com/a/grass_ground",
                       "authors": terrain_info.get("authors", {}), "license": "CC0-1.0",
                       "license_url": "https://polyhaven.com/license", "texture_resolution": "2K",
                       "files": ground_files}
    ground_path.write_text(
        json.dumps(ground_manifest, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    download_biome_textures()


if __name__ == "__main__":
    main()
