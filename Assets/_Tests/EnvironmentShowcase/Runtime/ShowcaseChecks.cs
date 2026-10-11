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
            public string[] implemented = { "Six real PBR layers", "Actual isolated terrain slope blending", "Semantic shore/road/forest soils",
                "World-scale low-contrast macro colour variation", "Two shifted neighbour samples for tile breakup",
                "Distance-faded source detail normals", "Chunked source mesh instancing and authored source LODs" };
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
            report.rock_pixel_pass = report.rock_instances > 0 && report.rock_mean_rgb_delta > .00001f;
            report.timings = timings.ToArray();
            report.grass_pixel_pass = report.grass_intentionally_absent ? !report.grass_available && report.grass_mean_rgb_delta < .00001f :
                report.grass_available && report.grass_mean_rgb_delta > .0003f;
            report.terrain_passed = report.terrain_pixel_pass && report.actual_slope_pass && report.layer_pass && report.shader_pass;
            report.source_complete = (report.grass_intentionally_absent || report.grass_available) &&
                (report.trees_intentionally_absent || report.trees_available) &&
                (showcase.current_biome == null || showcase.current_biome.shrub_density == 0 || report.shrub_instances > 0) &&
                report.rock_instances > 0 && !string.IsNullOrWhiteSpace(report.rock_source) &&
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

        private float compare_same_frame_rocks()
        {
            bool previous = showcase.foliage.show_rocks;
            showcase.foliage.show_rocks = true;
            var visible = capture_camera();
            Texture2D hidden = null;
            try
            {
                showcase.foliage.show_rocks = false;
                hidden = capture_camera();
                File.WriteAllBytes(Path.Combine(output_directory, "rocks-visible.png"), visible.EncodeToPNG());
                File.WriteAllBytes(Path.Combine(output_directory, "rocks-hidden.png"), hidden.EncodeToPNG());
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

        private float compare_same_frame_terrain()
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
                File.WriteAllBytes(Path.Combine(output_directory, "terrain-visible.png"), visible.EncodeToPNG());
                File.WriteAllBytes(Path.Combine(output_directory, "terrain-hidden.png"), hidden.EncodeToPNG());
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

        private static TimingSample build_timing(float[] samples, int zoom, bool grass_enabled)
        {
            Array.Sort(samples);
            return new TimingSample { view = "forest-" + new[] { "near", "middle", "far" }[zoom], grass_enabled = grass_enabled,
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
