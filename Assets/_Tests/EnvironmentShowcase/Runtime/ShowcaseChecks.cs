using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace ZombieGame.Tests.EnvironmentShowcase
{
    /// <summary>Packaged-player pixel contribution, actual slope, source completeness and same-view art timing evidence.</summary>
    public sealed class ShowcaseChecks : MonoBehaviour
    {
        [Serializable] private sealed class CheckReport
        {
            public string utc_time, unity_version, platform, device, processor, graphics, resolution;
            public string scope = "Isolated environment art review; not a gameplay/NavMesh or battle performance result.";
            public bool terrain_pixel_pass, actual_slope_pass, layer_pass, shader_pass, grass_available, trees_available;
            public bool passed;
            public bool terrain_passed, source_complete;
            public bool grass_pixel_pass;
            public float terrain_mean_rgb_delta, maximum_slope_degrees;
            public float grass_mean_rgb_delta;
            public float[] layer_mean_weights;
            public int grass_instances, tree_instances, shrub_instances, rock_instances;
            public string grass_source, tree_source, shrub_source, rock_source;
            public string ground_source, terrain_shader;
            public string biome, biome_label, unavailable_art;
            public string[] layer_semantics;
            public bool grass_intentionally_absent, trees_intentionally_absent, rock_pixel_pass, run_in_background;
            public float rock_mean_rgb_delta;
            public int target_frame_rate, shadow_cascades;
            public float shadow_distance, tree_shadow_draw_distance, meadow_grass_mean_rgb_delta;
            public bool height_boundary_pass, cliff_slope_pass, plateau_flat_pass, preserved_landmarks_pass, talus_transition_pass, cliff_pixel_pass, collider_height_pass;
            public float minimum_height_m, maximum_height_m, plateau_top_height_m, plateau_top_height_span_m, plateau_top_slope_degrees;
            public float preserved_landmark_max_delta_m, maximum_talus_weight, cliff_mean_rgb_delta;
            public string cliff_source, talus_source;
            public string previous_approved_views;
            public bool cliff_material_pixel_pass, talus_material_pixel_pass;
            public float cliff_material_mean_rgb_delta, talus_material_mean_rgb_delta;
            public string[] implemented = { "Six real PBR layers", "Actual isolated terrain slope blending", "Semantic shore/road/forest soils",
                "World-scale low-contrast macro colour variation", "Two shifted neighbour samples for tile breakup",
                "Distance-faded source detail normals", "Chunked source mesh instancing and authored source LODs",
                "Actual NE flat-topped plateau and steep cliff", "Desert weathered secondary ledge", "Local steep Stone triplanar PBR", "Semantic talus foot and source rocks" };
            public string[] deferred = { "Height blending: trustworthy six-layer height channels unavailable", "Terrain Shader Graph: Built-in pipeline unchanged",
                "Battle/navigation/unit-feedback regressions: run existing production checks separately" };
            public TimingSample[] timings;
            public string error;
        }

        [Serializable] private sealed class AggregateReport
        {
            public string utc_time, unity_version, platform, device, processor, graphics, resolution;
            public string scope = "Isolated environment art review; not a gameplay/NavMesh or battle performance result.";
            public bool passed, terrain_passed, source_complete, grass_pixel_pass, rock_pixel_pass;
            public CheckReport[] biomes;
        }

        [Serializable] private sealed class TimingSample
        {
            public string view;
            public bool grass_enabled;
            public int frames;
            public float median_ms, p95_ms;
        }

        private EnvironmentShowcase showcase;
        private CheckReport report;
        private string output_directory;
        private readonly List<TimingSample> timings = new List<TimingSample>();

        private IEnumerator Start()
        {
            var checks = run_checks();
            while (true)
            {
                bool has_next = false;
                Exception failure = null;
                try { has_next = checks.MoveNext(); }
                catch (Exception exception) { failure = exception; }
                if (failure != null)
                {
                    Debug.LogException(failure);
                    if (report != null && !string.IsNullOrEmpty(output_directory) && Directory.Exists(output_directory))
                    {
                        report.error = failure.ToString();
                        report.passed = false;
                        finish_report();
                    }
                    else Application.Quit(1);
                    yield break;
                }
                if (!has_next) yield break;
                yield return checks.Current;
            }
        }

        private IEnumerator run_checks()
        {
            showcase = GetComponent<EnvironmentShowcase>();
            output_directory = read_output_directory();
            Directory.CreateDirectory(output_directory);
            report = create_report();
            yield return null;
            if (!showcase || !showcase.enabled || showcase.ground == null)
            {
                report.error = "Showcase initialization failed; inspect Player.log.";
                finish_report();
                yield break;
            }
            string root_directory = output_directory;
            var results = new List<CheckReport>();
            foreach (int biome_index in read_biome_indices())
            {
                showcase.set_biome(biome_index);
                var profile = showcase.current_biome;
                output_directory = Path.Combine(root_directory, profile == null ? "baseline" : profile.id);
                Directory.CreateDirectory(output_directory);
                report = create_report();
                report.biome = profile == null ? "baseline" : profile.id;
                report.biome_label = profile == null ? "Baseline" : profile.label;
                report.unavailable_art = profile?.missing_art;
                report.previous_approved_views = "Builds/ArtReview/EnvironmentShowcase/biomes-final/" + report.biome + "/view-0.png through view-4.png; compare new same-resolution view-0..4, same exposure/camera settings.";
                report.layer_semantics = profile == null ? new[] { "Grass_A", "Grass_Dry", "Dirt", "Mud", "Stone", "Road" } : profile.layer_semantics;
                report.grass_intentionally_absent = profile != null && profile.grass_density == 0;
                report.trees_intentionally_absent = profile != null && profile.tree_density == 0;
                report.run_in_background = Application.runInBackground;
                report.target_frame_rate = Application.targetFrameRate;
                report.shadow_distance = QualitySettings.shadowDistance;
                report.shadow_cascades = QualitySettings.shadowCascades == 0 ? 1 : QualitySettings.shadowCascades;
                report.tree_shadow_draw_distance = showcase.config.tree_shadow_draw_distance;
                timings.Clear();
                var checks = run_biome_checks();
                while (checks.MoveNext()) yield return checks.Current;
                File.WriteAllText(Path.Combine(output_directory, "showcase-report.json"), JsonUtility.ToJson(report, true));
                results.Add(report);
            }
            output_directory = root_directory;
            finish_aggregate_report(results);
        }

        private void finish_aggregate_report(List<CheckReport> results)
        {
            var aggregate = new AggregateReport { utc_time = DateTime.UtcNow.ToString("O"), unity_version = Application.unityVersion,
                platform = Application.platform.ToString(), device = SystemInfo.deviceModel, processor = SystemInfo.processorType,
                graphics = SystemInfo.graphicsDeviceName, resolution = Screen.width + "x" + Screen.height,
                biomes = results.ToArray(), passed = true, terrain_passed = true, source_complete = true, grass_pixel_pass = true, rock_pixel_pass = true };
            foreach (var result in results)
            {
                aggregate.passed &= result.passed;
                aggregate.terrain_passed &= result.terrain_passed;
                aggregate.source_complete &= result.source_complete;
                aggregate.grass_pixel_pass &= result.grass_pixel_pass;
                aggregate.rock_pixel_pass &= result.rock_pixel_pass;
            }
            File.WriteAllText(Path.Combine(output_directory, "showcase-report.json"), JsonUtility.ToJson(aggregate, true));
            Debug.Log("[EnvironmentShowcaseCheck] " + (aggregate.passed ? "REGIONS PASS" : "REGIONS FAIL") +
                "; regions=" + aggregate.biomes.Length + "; terrain=" + aggregate.terrain_passed + "; source=" + aggregate.source_complete +
                "; report=" + output_directory);
            Application.Quit(aggregate.passed ? 0 : 1);
        }

        private int[] read_biome_indices()
        {
            string requested = "all";
            var arguments = Environment.GetCommandLineArgs();
            for (int index = 0; index + 1 < arguments.Length; index++)
                if (arguments[index] == "-showcaseBiome") requested = arguments[index + 1].ToLowerInvariant();
            int count = showcase.config.biomes == null || showcase.config.biomes.Length == 0 ? 1 : showcase.config.biomes.Length;
            if (requested == "all")
            {
                var indices = new int[count];
                for (int index = 0; index < count; index++) indices[index] = index;
                return indices;
            }
            for (int index = 0; index < count; index++)
                if (requested == index.ToString() || (showcase.config.biomes != null && showcase.config.biomes.Length > 0 &&
                    (requested == showcase.config.biomes[index].id || requested == showcase.config.biomes[index].label.ToLowerInvariant()))) return new[] { index };
            throw new InvalidOperationException("Unknown -showcaseBiome: " + requested + "; use all / europe / mountains / winter / desert.");
        }

        private IEnumerator run_biome_checks()
        {
            showcase.show_hud = false;
            showcase.foliage.show_grass = true;
            inspect_ground();
            inspect_landforms();
            showcase.set_view(1, 1);
            for (int frame = 0; frame < 12; frame++) yield return null;
            yield return new WaitForEndOfFrame();
            report.terrain_mean_rgb_delta = compare_same_frame_terrain();
            report.terrain_pixel_pass = report.terrain_mean_rgb_delta > .006f;
            for (int view_index = 0; view_index < EnvironmentShowcase.VIEW_NAMES.Length; view_index++)
            {
                showcase.set_view(view_index, view_index == 0 ? 2 : 0);
                for (int frame = 0; frame < 12; frame++) yield return null;
                yield return new WaitForEndOfFrame();
                save_camera_image("view-" + view_index + ".png");
            }
            showcase.set_view(5, 0);
            yield return new WaitForEndOfFrame();
            report.cliff_mean_rgb_delta = compare_same_frame_terrain("cliff");
            report.cliff_pixel_pass = report.cliff_mean_rgb_delta > .006f;
            report.cliff_material_mean_rgb_delta = compare_same_frame_material("_HasCliffPbr", "cliff-material");
            report.cliff_material_pixel_pass = report.cliff_material_mean_rgb_delta > .0001f;
            var landform_timings = sample_cliff_timings();
            while (landform_timings.MoveNext()) yield return landform_timings.Current;
            for (int zoom = 0; zoom < 3; zoom++)
            {
                showcase.set_view(3, zoom);
                Color32[] grass_on_pixels = null;
                foreach (bool grass_enabled in new[] { true, false })
                {
                    showcase.foliage.show_grass = grass_enabled;
                    for (int frame = 0; frame < 60; frame++) yield return null;
                    var samples = new float[180];
                    for (int frame = 0; frame < samples.Length; frame++)
                    {
                        yield return null;
                        samples[frame] = Time.unscaledDeltaTime * 1000;
                    }
                    timings.Add(build_timing(samples, zoom, grass_enabled));
                    yield return new WaitForEndOfFrame();
                    var capture = capture_camera();
                    try
                    {
                        var pixels = capture.GetPixels32();
                        if (grass_enabled) grass_on_pixels = pixels;
                        else report.grass_mean_rgb_delta = Mathf.Max(report.grass_mean_rgb_delta, measure_pixel_delta(grass_on_pixels, pixels));
                        File.WriteAllBytes(Path.Combine(output_directory, "forest-" + zoom +
                            (grass_enabled ? "-grass-on.png" : "-grass-off.png")), capture.EncodeToPNG());
                    }
                    finally { Destroy(capture); }
                }
            }
            showcase.set_view(0, 0);
            for (int frame = 0; frame < 12; frame++) yield return null;
            yield return new WaitForEndOfFrame();
            report.meadow_grass_mean_rgb_delta = compare_same_frame_meadow_grass();
            report.grass_mean_rgb_delta = Mathf.Max(report.grass_mean_rgb_delta, report.meadow_grass_mean_rgb_delta);
            showcase.set_view(4, 0);
            for (int frame = 0; frame < 12; frame++) yield return null;
            yield return new WaitForEndOfFrame();
            report.rock_mean_rgb_delta = compare_same_frame_rocks();
            showcase.set_view(7, 0);
            for (int frame = 0; frame < 12; frame++) yield return null;
            yield return new WaitForEndOfFrame();
            report.rock_mean_rgb_delta = Mathf.Max(report.rock_mean_rgb_delta, compare_same_frame_rocks("talus"));
            report.talus_material_mean_rgb_delta = compare_same_frame_material("_HasTalusPbr", "talus-material");
            report.talus_material_pixel_pass = report.talus_material_mean_rgb_delta > .0001f;
            report.rock_pixel_pass = report.rock_instances > 0 && report.rock_mean_rgb_delta > .00001f;
            report.timings = timings.ToArray();
            report.grass_pixel_pass = report.grass_intentionally_absent ? !report.grass_available && report.grass_mean_rgb_delta < .00001f :
                report.grass_available && report.grass_mean_rgb_delta > .0003f;
            report.terrain_passed = report.terrain_pixel_pass && report.actual_slope_pass && report.layer_pass && report.shader_pass &&
                report.height_boundary_pass && report.collider_height_pass && report.cliff_slope_pass && report.plateau_flat_pass &&
                report.preserved_landmarks_pass && report.talus_transition_pass && report.cliff_pixel_pass &&
                report.cliff_material_pixel_pass && report.talus_material_pixel_pass;
            report.source_complete = (report.grass_intentionally_absent || report.grass_available) &&
                (report.trees_intentionally_absent || report.trees_available) &&
                (showcase.current_biome == null || showcase.current_biome.shrub_density == 0 || report.shrub_instances > 0) &&
                report.rock_instances > 0 && !string.IsNullOrWhiteSpace(report.rock_source) &&
                showcase.current_biome != null && showcase.current_biome.cliff_albedo_smoothness && showcase.current_biome.cliff_normal &&
                showcase.current_biome.talus_albedo_smoothness && showcase.current_biome.talus_normal &&
                !string.IsNullOrWhiteSpace(report.cliff_source) && !string.IsNullOrWhiteSpace(report.talus_source) &&
                showcase.config.grass_layer_override && showcase.config.terrain_material && !string.IsNullOrWhiteSpace(report.ground_source);
            report.passed = report.terrain_passed && report.source_complete && report.grass_pixel_pass && report.rock_pixel_pass;
        }

        private float compare_same_frame_meadow_grass()
        {
            bool previous = showcase.foliage.show_grass;
            showcase.foliage.show_grass = true;
            var visible = capture_camera();
            Texture2D hidden = null;
            try
            {
                showcase.foliage.show_grass = false;
                hidden = capture_camera();
                File.WriteAllBytes(Path.Combine(output_directory, "meadow-grass-on.png"), visible.EncodeToPNG());
                File.WriteAllBytes(Path.Combine(output_directory, "meadow-grass-off.png"), hidden.EncodeToPNG());
                return measure_pixel_delta(visible.GetPixels32(), hidden.GetPixels32());
            }
            finally { showcase.foliage.show_grass = previous; Destroy(visible); if (hidden) Destroy(hidden); }
        }

        private float compare_same_frame_rocks(string prefix = "rocks")
        {
            bool previous = showcase.foliage.show_rocks;
            showcase.foliage.show_rocks = true;
            var visible = capture_camera();
            Texture2D hidden = null;
            try
            {
                showcase.foliage.show_rocks = false;
                hidden = capture_camera();
                File.WriteAllBytes(Path.Combine(output_directory, prefix + "-visible.png"), visible.EncodeToPNG());
                File.WriteAllBytes(Path.Combine(output_directory, prefix + "-hidden.png"), hidden.EncodeToPNG());
                return measure_pixel_delta(visible.GetPixels32(), hidden.GetPixels32());
            }
            finally { showcase.foliage.show_rocks = previous; Destroy(visible); if (hidden) Destroy(hidden); }
        }

        private static string read_output_directory()
        {
            var arguments = Environment.GetCommandLineArgs();
            for (int index = 0; index + 1 < arguments.Length; index++)
                if (arguments[index] == "-showcaseOutput") return Path.GetFullPath(arguments[index + 1]);
            return Path.Combine(Application.persistentDataPath, "EnvironmentShowcase", DateTime.UtcNow.ToString("yyyyMMdd-HHmmss"));
        }

        private CheckReport create_report()
        {
            return new CheckReport { utc_time = DateTime.UtcNow.ToString("O"), unity_version = Application.unityVersion,
                platform = Application.platform.ToString(), device = SystemInfo.deviceModel, processor = SystemInfo.processorType,
                graphics = SystemInfo.graphicsDeviceName, resolution = Screen.width + "x" + Screen.height };
        }

        private void inspect_ground()
        {
            var terrain_data = showcase.ground.terrain_data;
            report.layer_mean_weights = new float[6];
            report.layer_pass = terrain_data.terrainLayers.Length == 6;
            report.shader_pass = showcase.ground.terrain.materialTemplate && showcase.ground.terrain.materialTemplate.shader &&
                showcase.ground.terrain.materialTemplate.shader.isSupported;
            const int SAMPLE_GRID = 40;
            var weights = new float[6];
            for (int z = 0; z < SAMPLE_GRID; z++)
                for (int x = 0; x < SAMPLE_GRID; x++)
                {
                    float u = x / (float)(SAMPLE_GRID - 1), v = z / (float)(SAMPLE_GRID - 1);
                    report.maximum_slope_degrees = Mathf.Max(report.maximum_slope_degrees, terrain_data.GetSteepness(u, v));
                    showcase.ground.sample_weights(u, v, weights);
                    float sum = 0;
                    for (int index = 0; index < weights.Length; index++)
                    {
                        if (float.IsNaN(weights[index]) || weights[index] < 0) report.layer_pass = false;
                        report.layer_mean_weights[index] += weights[index] / (SAMPLE_GRID * SAMPLE_GRID);
                        sum += weights[index];
                    }
                    if (Mathf.Abs(sum - 1) > .001f) report.layer_pass = false;
                }
            foreach (float mean_weight in report.layer_mean_weights) if (mean_weight <= .003f) report.layer_pass = false;
            report.actual_slope_pass = report.maximum_slope_degrees > (showcase.current_biome == null ? showcase.config.slope_rock_start : showcase.current_biome.rock_start) + 8;
            report.grass_instances = showcase.foliage.grass_instances;
            report.tree_instances = showcase.foliage.tree_instances;
            report.shrub_instances = showcase.foliage.shrub_instances;
            report.rock_instances = showcase.foliage.rock_instances;
            report.grass_available = report.grass_instances > 0;
            report.trees_available = report.tree_instances > 0;
            report.grass_source = showcase.config.grass_source_record;
            report.tree_source = showcase.config.tree_source_record;
            report.shrub_source = showcase.config.shrub_source_record;
            report.rock_source = showcase.config.rock_source_record;
            report.ground_source = showcase.config.ground_source_record + "; " + showcase.current_biome?.source_record;
            report.terrain_shader = showcase.ground.terrain.materialTemplate.shader.name;
        }

        /// <summary>Full native heightfield checks, explicit flat cap, true cliff gradients and protected original view landmarks.</summary>
        private void inspect_landforms()
        {
            inspect_height_bounds();
            inspect_plateau_top();
            inspect_preserved_landmarks();
            inspect_talus_transition();
        }

        private void inspect_height_bounds()
        {
            var ground = showcase.ground;
            var terrain_data = ground.terrain_data;
            report.height_boundary_pass = true;
            report.minimum_height_m = float.PositiveInfinity;
            report.maximum_height_m = float.NegativeInfinity;
            int resolution = terrain_data.heightmapResolution;
            var heights = terrain_data.GetHeights(0, 0, resolution, resolution);
            for (int z = 0; z < resolution; z++)
                for (int x = 0; x < resolution; x++)
                {
                    float height = heights[z, x] * terrain_data.size.y;
                    if (float.IsNaN(height) || height < 0 || height >= terrain_data.size.y - .1f) report.height_boundary_pass = false;
                    report.minimum_height_m = Mathf.Min(report.minimum_height_m, height);
                    report.maximum_height_m = Mathf.Max(report.maximum_height_m, height);
                    report.maximum_slope_degrees = Mathf.Max(report.maximum_slope_degrees, terrain_data.GetSteepness(x / (float)(resolution - 1), z / (float)(resolution - 1)));
                }
            var collider = ground.terrain.GetComponent<TerrainCollider>();
            report.collider_height_pass = collider && collider.terrainData == terrain_data;
            report.cliff_slope_pass = report.maximum_slope_degrees > 65;
        }

        private void inspect_plateau_top()
        {
            var profile = showcase.current_biome;
            var terrain_data = showcase.ground.terrain_data;
            if (profile == null || !profile.plateau_enabled) return;
            report.cliff_source = profile.cliff_source_record;
            report.talus_source = profile.talus_source_record;
            float top_min = float.PositiveInfinity, top_max = float.NegativeInfinity;
            for (int z = -1; z <= 1; z++)
                for (int x = -1; x <= 1; x++)
                {
                    float u = profile.plateau_center.x + x * profile.plateau_radius.x * .22f / showcase.config.map_size;
                    float v = profile.plateau_center.y + z * profile.plateau_radius.y * .22f / showcase.config.map_size;
                    float height = terrain_data.GetInterpolatedHeight(u, v);
                    top_min = Mathf.Min(top_min, height);
                    top_max = Mathf.Max(top_max, height);
                    report.plateau_top_slope_degrees = Mathf.Max(report.plateau_top_slope_degrees, terrain_data.GetSteepness(u, v));
                }
            report.plateau_top_height_m = terrain_data.GetInterpolatedHeight(profile.plateau_center.x, profile.plateau_center.y);
            report.plateau_top_height_span_m = top_max - top_min;
            report.plateau_flat_pass = top_max - top_min < .12f && report.plateau_top_slope_degrees < 5 &&
                Mathf.Abs(report.plateau_top_height_m - profile.plateau_height) < .06f;
        }

        private void inspect_preserved_landmarks()
        {
            var ground = showcase.ground;
            var terrain_data = ground.terrain_data;
            foreach (var point in new[] { new Vector2(.5f, .53f), new Vector2(.42f, .48f), new Vector2(.43f, .235f), new Vector2(.61f, .57f), new Vector2(.70f, .72f) })
                report.preserved_landmark_max_delta_m = Mathf.Max(report.preserved_landmark_max_delta_m,
                    Mathf.Abs(terrain_data.GetInterpolatedHeight(point.x, point.y) - ground.sample_original_height(point.x * showcase.config.map_size, point.y * showcase.config.map_size)));
            report.preserved_landmarks_pass = report.preserved_landmark_max_delta_m < .06f;
        }

        private void inspect_talus_transition()
        {
            var ground = showcase.ground;
            var profile = showcase.current_biome;
            if (profile == null || !profile.plateau_enabled) return;
            var weights = new float[6];
            report.talus_transition_pass = false;
            for (int index = 0; index < 36; index++)
            {
                float angle = index * Mathf.PI * 2 / 36;
                float radius = 1 + (Mathf.Sin(angle * 7) + Mathf.Sin(angle * 13) * .4f) * profile.erosion_strength;
                float physical_radius = new Vector2(Mathf.Cos(angle) * profile.plateau_radius.x,
                    Mathf.Sin(angle) * profile.plateau_radius.y).magnitude;
                radius += (2 + (profile.dry_basin ? profile.cliff_edge_width * 2 : 0)) / physical_radius;
                float u = profile.plateau_center.x + Mathf.Cos(angle) * profile.plateau_radius.x * radius / showcase.config.map_size;
                float v = profile.plateau_center.y + Mathf.Sin(angle) * profile.plateau_radius.y * radius / showcase.config.map_size;
                if (u < 0 || v < 0 || u > 1 || v > 1) continue;
                float talus = ground.talus_weight(u * showcase.config.map_size, v * showcase.config.map_size);
                report.maximum_talus_weight = Mathf.Max(report.maximum_talus_weight, talus);
                ground.sample_weights(u, v, weights);
                if (talus > .6f && weights[4] > .2f && weights[2] > .01f) report.talus_transition_pass = true;
            }
        }

        /// <summary>Toggle only one local PBR contribution; leave terrain geometry and all other draws intact.</summary>
        private float compare_same_frame_material(string property_name, string prefix)
        {
            var terrain = showcase.ground.terrain;
            var material = terrain.materialTemplate;
            if (!material || !material.HasProperty(property_name) || material.GetFloat(property_name) < .5f)
                throw new InvalidOperationException("Showcase material check requires enabled source PBR: " + property_name);
            float previous = material.GetFloat(property_name);
            Texture2D visible = null, hidden = null;
            try
            {
                terrain.Flush();
                visible = capture_camera();
                material.SetFloat(property_name, 0);
                terrain.Flush();
                hidden = capture_camera();
                File.WriteAllBytes(Path.Combine(output_directory, prefix + "-on.png"), visible.EncodeToPNG());
                File.WriteAllBytes(Path.Combine(output_directory, prefix + "-off.png"), hidden.EncodeToPNG());
                return measure_pixel_delta(visible.GetPixels32(), hidden.GetPixels32());
            }
            finally
            {
                material.SetFloat(property_name, previous);
                terrain.Flush();
                if (visible) Destroy(visible);
                if (hidden) Destroy(hidden);
            }
        }

        private float compare_same_frame_terrain(string prefix = "terrain")
        {
            var terrain = showcase.ground.terrain;
            bool previous_heightmap = terrain.drawHeightmap;
            var visible = capture_camera();
            Texture2D hidden = null;
            try
            {
                terrain.drawHeightmap = false;
                hidden = capture_camera();
                var visible_pixels = visible.GetPixels32();
                var hidden_pixels = hidden.GetPixels32();
                File.WriteAllBytes(Path.Combine(output_directory, prefix + "-visible.png"), visible.EncodeToPNG());
                File.WriteAllBytes(Path.Combine(output_directory, prefix + "-hidden.png"), hidden.EncodeToPNG());
                return measure_pixel_delta(visible_pixels, hidden_pixels);
            }
            finally
            {
                terrain.drawHeightmap = previous_heightmap;
                Destroy(visible);
                if (hidden) Destroy(hidden);
            }
        }

        private static float measure_pixel_delta(Color32[] first, Color32[] second)
        {
            if (first == null || second == null || first.Length != second.Length)
                throw new InvalidOperationException("Showcase pixel comparison needs two matching framebuffers.");
            double sum = 0;
            for (int index = 0; index < first.Length; index++)
                sum += Mathf.Abs(first[index].r - second[index].r) + Mathf.Abs(first[index].g - second[index].g) +
                    Mathf.Abs(first[index].b - second[index].b);
            return (float)(sum / (first.Length * 3 * 255.0));
        }

        private Texture2D capture_camera()
        {
            var camera = showcase.review_camera;
            var previous_target = camera.targetTexture;
            var previous_active = RenderTexture.active;
            var target = RenderTexture.GetTemporary(Screen.width, Screen.height, 24, RenderTextureFormat.ARGB32);
            try
            {
                camera.targetTexture = target;
                camera.Render();
                RenderTexture.active = target;
                var texture = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
                texture.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
                texture.Apply();
                return texture;
            }
            finally
            {
                camera.targetTexture = previous_target;
                RenderTexture.active = previous_active;
                RenderTexture.ReleaseTemporary(target);
            }
        }

        private void save_camera_image(string file_name)
        {
            var image = capture_camera();
            try { File.WriteAllBytes(Path.Combine(output_directory, file_name), image.EncodeToPNG()); }
            finally { Destroy(image); }
        }

        private IEnumerator sample_cliff_timings()
        {
            for (int zoom = 0; zoom < 2; zoom++)
            {
                showcase.set_view(5, zoom);
                for (int frame = 0; frame < 60; frame++) yield return null;
                var samples = new float[180];
                for (int frame = 0; frame < samples.Length; frame++)
                {
                    yield return null;
                    samples[frame] = Time.unscaledDeltaTime * 1000;
                }
                timings.Add(build_timing(samples, zoom, true, "cliff-" + (zoom == 0 ? "near" : "middle")));
                yield return new WaitForEndOfFrame();
                save_camera_image("cliff-" + (zoom == 0 ? "near" : "middle") + "-timing.png");
            }
        }

        private static TimingSample build_timing(float[] samples, int zoom, bool grass_enabled, string view_name = null)
        {
            Array.Sort(samples);
            return new TimingSample { view = view_name ?? "forest-" + new[] { "near", "middle", "far" }[zoom], grass_enabled = grass_enabled,
                frames = samples.Length, median_ms = samples[samples.Length / 2],
                p95_ms = samples[Mathf.CeilToInt(samples.Length * .95f) - 1] };
        }

        private void finish_report()
        {
            File.WriteAllText(Path.Combine(output_directory, "showcase-report.json"), JsonUtility.ToJson(report, true));
            Debug.Log("[EnvironmentShowcaseCheck] " + (report.passed ? "PREVIEW PASS" : "FAIL") + "; terrain=" + report.terrain_passed +
                "; source complete=" + report.source_complete + "; grass source=" + report.grass_available +
                "; trees source=" + report.trees_available + "; report=" + output_directory);
            Application.Quit(report.passed ? 0 : 1);
        }
    }
}
