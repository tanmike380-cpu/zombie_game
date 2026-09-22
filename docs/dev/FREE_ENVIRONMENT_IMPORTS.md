# Three free environment samples

## Sources and scope

Powered by Poly Haven. Assets are CC0-1.0: https://polyhaven.com/license

| Sample | Source | Use |
| --- | --- | --- |
| Grass Path 2 | https://polyhaven.com/a/grass_path_2 | Ground albedo and roughness |
| Rock Moss Set 01 | https://polyhaven.com/a/rock_moss_set_01 | Actual FBX rock group, albedo, normal and roughness |
| Plastered Stone Wall | https://polyhaven.com/a/plastered_stone_wall | Existing Eastern building wall albedo and roughness |

These are three asset sets, **not three complete game art packs**. Soldiers, zombies, tree models and building geometry have not been replaced. They are deliberately not substituted with modern soldiers or Western architecture. The previously suggested Unity Asset Store packages were not downloaded or imported.

## Reproduction

From the repository root on macOS, run `bash tools/download_free_environment.sh` before opening Unity or building. Requires `curl`, `jq` and macOS `md5`.

- `art/free_environment_manifest.json` pins each URL and MD5. The script verifies every file and refuses to overwrite a locally modified asset.
- Downloaded assets live in `Assets/_Game/Resources/FreeEnvironment`, excluded from Git to avoid accumulating raw binary packs; the manifest, import settings and integration code are versioned. Other checkouts must run the download step too.
- Texture importer uses 1K, mipmaps, anisotropic filtering, appropriate normal-map and linear roughness settings.
- `FreeEnvironmentArt` creates bounded, distance-culled rock groups with shared materials. No added colliders or gameplay stats; the existing map/navigation footprint is unchanged.
- `FrontierSurface` projects textures in world space on existing ground/building batches; imported rocks use their authored UVs. Imported terrain/wall normal maps are available but not currently applied to the world-projected surfaces.
- Missing downloads fall back to existing art with a warning. Pass `-legacyEnvironment` for an explicit original-art comparison in the same application.

## Verification

`-freeEnvironmentSmoke` checks supported materials, all three sets connected, bounded rock groups, no imported colliders and unchanged population. It saves `/tmp/zombie-environment-cc0.png` and logs an 8-second startup frame-time sample. Repeat with `-legacyEnvironment` for `/tmp/zombie-environment-legacy.png`.

The startup sample is not a siege benchmark and does not establish minimum specifications. Continue to run existing `-frontierSmoke` and `-unitUiSmoke` to guard accepted gameplay/UI behavior.

### Observed 2026-09-22

- Unity 6000.6.1f1 macOS build passed. Actual rendered screenshots inspected: textured ground/walls and six distinct source rock meshes visible at the nearby stone deposit, no pink error materials.
- 40 textured renderers/material slots and 5 rock groups were found. Both environment modes passed their checks.
- Fixed base-center camera, orthographic size 24, 2704×1486, 400 selected humans/5000 zombies, 8-second sample: CC0 median **50.00 ms**, p95 **83.02 ms**; legacy median **50.00 ms**, p95 **50.31 ms**. Existing base view is already roughly 20 FPS; this does **not** pass a 60 FPS target and the new sample showed worse tail latency. More profiling is required before scaling up art.
- An earlier freely drifting camera saw a median 16.67 ms but drifted away from the base; that is not the relevant comparison and must not be reported as base-view performance.
- Existing frontier checks passed (minimap drag/commands, selection focus, building placement, supply, geometry=0). Conditional HP/ammo bars, empty-ammo cue, selected rings and shared building sight passed `-unitUiSmoke`.
