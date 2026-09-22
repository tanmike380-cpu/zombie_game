#!/bin/bash
# Download the three pinned CC0 environment samples; never silently replace modified files.
set -euo pipefail
project_dir="$(cd "$(dirname "$0")/.." && pwd)"
manifest_path="$project_dir/art/free_environment_manifest.json"
asset_dir="$project_dir/Assets/_Game/Resources/FreeEnvironment"
while IFS=$'\t' read -r pack_id file_name expected_md5 download_url; do
    target_path="$asset_dir/$pack_id/$file_name"
    if [ ! -f "$target_path" ]; then
        mkdir -p "$asset_dir/$pack_id"
        curl --fail --location --retry 2 --connect-timeout 15 --max-time 180 --silent --show-error "$download_url" --output "$target_path.download"
        actual_md5="$(md5 -q "$target_path.download")"
        if [ "$actual_md5" != "$expected_md5" ]; then
            echo "Checksum mismatch: $target_path.download" >&2
            exit 1
        fi
        mv "$target_path.download" "$target_path"
    fi
    if [ "$(md5 -q "$target_path")" != "$expected_md5" ]; then
        echo "Existing asset modified; preserving it: $target_path" >&2
        exit 1
    fi
    echo "Verified $pack_id/$file_name"
done < <(jq -r '.packs[] | .id as $id | .files[] | [$id,.name,.md5,.url] | @tsv' "$manifest_path")
