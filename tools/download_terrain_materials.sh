#!/bin/bash
# Pinned CC0 texture downloads, preserving locally modified files.
set -euo pipefail
project_dir="$(cd "$(dirname "$0")/.." && pwd)"
manifest_path="$project_dir/art/terrain_material_manifest.json"
download_root="$(jq -r '.download_root' "$manifest_path")"
asset_dir="$project_dir/$(jq -r '.destination' "$manifest_path")"
download_file() {
    local pack_id="$1" channel="$2" expected_md5="$3"
    file_name="${pack_id}_${channel}_1k.jpg"
    target_path="$asset_dir/$pack_id/$file_name"
    if [ ! -f "$target_path" ]; then
        mkdir -p "$asset_dir/$pack_id"
        curl --fail --location --continue-at - --retry 1 --connect-timeout 15 --max-time 300 --silent --show-error "$download_root/$pack_id/$file_name" --output "$target_path.download"
        if [ "$(md5 -q "$target_path.download")" != "$expected_md5" ]; then
            echo "Terrain checksum mismatch; preserving download for inspection: $target_path.download" >&2
            exit 1
        fi
        mv "$target_path.download" "$target_path"
    fi
    if [ "$(md5 -q "$target_path")" != "$expected_md5" ]; then
        echo "Terrain source modified locally; refusing to overwrite: $target_path" >&2
        exit 1
    fi
    echo "Verified $pack_id/$file_name"
}
export download_root asset_dir
export -f download_file
jq -r '.channels as $channels | .packs[] | .id as $id | .md5 | to_entries[] | [$id,$channels[.key],.value] | @tsv' "$manifest_path" |
    xargs -P 4 -n 3 bash -euc 'download_file "$@"' _
