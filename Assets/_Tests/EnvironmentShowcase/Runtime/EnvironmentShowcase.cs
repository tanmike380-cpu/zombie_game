using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace ZombieGame.Tests.EnvironmentShowcase
{
    /// <summary>Standalone ground-art review. No combat, unit fixtures or navigation changes.</summary>
    public sealed class EnvironmentShowcase : MonoBehaviour
    {
        public EnvironmentShowcaseConfig config;
        public ShowcaseTerrain ground { get; private set; }
        public ShowcaseFoliage foliage { get; private set; }
        public Camera review_camera { get; private set; }
        public bool show_hud = true;
        public int zoom_index { get; private set; } = 1;
        public int view_index { get; private set; }
        public int biome_index { get; private set; }
        public EnvironmentShowcaseConfig.BiomeProfile current_biome => config.biomes != null && config.biomes.Length > 0 ? config.biomes[biome_index] : null;
        private readonly float[] frame_samples = new float[600];
        private int frame_cursor;
        private int frame_count;
        private float median_ms;
        private float p95_ms;
        private float next_stats_time;
        private Material water_material;
        private int previous_vsync;
        private int previous_target_framerate;
        private bool previous_run_in_background;
        private float previous_shadow_distance;
        private int previous_shadow_cascades;
        private Light sun;
        public static readonly string[] VIEW_NAMES = { "Meadow overview", "Road seam", "Wet shoreline", "Forest edge", "Actual rocky slope", "Cliff face", "Plateau top", "Talus foot" };
        private static readonly Vector2[] VIEW_POINTS = { new Vector2(.5f, .53f), new Vector2(.42f, .48f),
            new Vector2(.43f, .235f), new Vector2(.61f, .57f), new Vector2(.70f, .72f) };

        private void Awake()
        {
            previous_vsync = QualitySettings.vSyncCount;
            previous_target_framerate = Application.targetFrameRate;
            previous_run_in_background = Application.runInBackground;
            previous_shadow_distance = QualitySettings.shadowDistance;
            previous_shadow_cascades = QualitySettings.shadowCascades;
            Application.runInBackground = true;
            try
            {
                if (!config) throw new InvalidOperationException("Environment showcase requires its independent authored config asset.");
                config.validate_config();
                configure_lighting();
                ground = new ShowcaseTerrain(config, transform, current_biome);
                foliage = new ShowcaseFoliage(config, ground);
                create_water();
                create_camera();
                Camera.onPreCull += submit_foliage;
                set_view(0, 1);
                QualitySettings.vSyncCount = 0;
                Application.targetFrameRate = config.target_frame_rate;
                QualitySettings.shadowDistance = config.shadow_distance;
                // Unity represents a single directional shadow map with zero cascades.
                QualitySettings.shadowCascades = config.shadow_cascades == 1 ? 0 : config.shadow_cascades;
                Debug.Log("[EnvironmentShowcase] Built-in six-layer PBR terrain, actual isolated hills; grass=" + foliage.grass_instances +
                    ", trees=" + foliage.tree_instances + ", shrubs=" + foliage.shrub_instances + ", rocks=" + foliage.rock_instances + ". No gameplay or NavMesh altered.");
                if (Array.IndexOf(Environment.GetCommandLineArgs(), "-showcaseCheck") >= 0)
                    gameObject.AddComponent<ShowcaseChecks>();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                enabled = false;
                if (Array.IndexOf(Environment.GetCommandLineArgs(), "-showcaseCheck") >= 0 && !GetComponent<ShowcaseChecks>())
                    gameObject.AddComponent<ShowcaseChecks>();
            }
        }

        private void configure_lighting()
        {
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = current_biome == null ? config.ambient_color : current_biome.ambient;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = current_biome == null ? config.fog_color : current_biome.fog;
            RenderSettings.fogStartDistance = config.fog_start;
            RenderSettings.fogEndDistance = config.fog_end;
            if (!sun)
            {
                var sun_object = new GameObject("Showcase afternoon sun");
                sun_object.transform.SetParent(transform, false);
                sun_object.transform.rotation = Quaternion.Euler(config.sun_rotation);
                sun = sun_object.AddComponent<Light>();
            }
            sun.type = LightType.Directional;
            sun.color = current_biome == null ? config.sun_color : current_biome.sun;
            sun.intensity = current_biome == null ? config.sun_intensity : current_biome.sun_intensity;
            sun.shadows = LightShadows.Soft;
            sun.shadowBias = .025f;
            sun.shadowNormalBias = .35f;
            RenderSettings.sun = sun;
        }

        private void create_water()
        {
            var water = GameObject.CreatePrimitive(PrimitiveType.Plane);
            water.name = "Basic water surface — not a replacement world model";
            water.transform.SetParent(transform, false);
            water.transform.position = new Vector3(0, config.water_level, -config.map_size * .38f);
            water.transform.localScale = new Vector3(config.map_size / 10, 1, config.map_size * .30f / 10);
            Destroy(water.GetComponent<Collider>());
            var shader = Shader.Find("Standard");
            if (!shader) throw new InvalidOperationException("Built-in Standard shader missing for basic showcase water.");
            water_material = new Material(shader) { name = "Showcase basic opaque water", color = current_biome == null ? config.water_color : current_biome.water };
            water_material.SetFloat("_Metallic", 0);
            water_material.SetFloat("_Glossiness", .72f);
            water.GetComponent<Renderer>().sharedMaterial = water_material;
        }

        private void create_camera()
        {
            var camera_object = new GameObject("Showcase fixed-exposure camera");
            camera_object.transform.SetParent(transform, false);
            camera_object.tag = "MainCamera";
            review_camera = camera_object.AddComponent<Camera>();
            review_camera.orthographic = true;
            review_camera.nearClipPlane = .1f;
            review_camera.farClipPlane = 500;
            review_camera.clearFlags = CameraClearFlags.SolidColor;
            review_camera.backgroundColor = current_biome == null ? config.fog_color : current_biome.fog;
            review_camera.allowHDR = false;
            review_camera.allowMSAA = true;
        }

        public void set_view(int requested_view, int requested_zoom)
        {
            view_index = Mathf.Clamp(requested_view, 0, VIEW_NAMES.Length - 1);
            zoom_index = Mathf.Clamp(requested_zoom, 0, config.camera_sizes.Length - 1);
            var view_point = view_index < VIEW_POINTS.Length ? VIEW_POINTS[view_index] : new Vector2(.77f, .81f);
            var profile = current_biome;
            if (view_index >= VIEW_POINTS.Length && profile != null)
            {
                view_point = profile.plateau_center;
                if (view_index != 6)
                    view_point.y -= (profile.plateau_radius.y + (view_index == 7 ? profile.talus_width * .45f +
                        (profile.dry_basin ? profile.cliff_edge_width * 2 : 0) : -profile.cliff_edge_width * .5f)) / config.map_size;
            }
            Vector3 focus = ground.sample_ground(view_point.x * config.map_size, view_point.y * config.map_size);
            Quaternion rotation = Quaternion.Euler(config.camera_pitch, config.camera_yaw, 0);
            review_camera.transform.SetPositionAndRotation(focus - rotation * Vector3.forward * config.camera_sizes[zoom_index] * 2.3f, rotation);
            review_camera.orthographicSize = config.camera_sizes[zoom_index];
            reset_frame_samples();
        }

        /// <summary>Switch authored region in the same review controller; dispose only isolated runtime resources.</summary>
        public void set_biome(int requested_biome)
        {
            if (config.biomes == null || config.biomes.Length == 0) return;
            biome_index = Mathf.Clamp(requested_biome, 0, config.biomes.Length - 1);
            bool grass_enabled = foliage == null || foliage.show_grass;
            foliage?.Dispose();
            ground?.Dispose();
            ground = new ShowcaseTerrain(config, transform, current_biome);
            foliage = new ShowcaseFoliage(config, ground) { show_grass = grass_enabled };
            configure_lighting();
            if (water_material)
            {
                water_material.color = current_biome.water;
                water_material.SetFloat("_Glossiness", current_biome.id == "winter" ? .2f : .72f);
            }
            review_camera.backgroundColor = current_biome.fog;
            set_view(view_index, zoom_index);
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Alpha1)) set_view(view_index, 0);
            if (Input.GetKeyDown(KeyCode.Alpha2)) set_view(view_index, 1);
            if (Input.GetKeyDown(KeyCode.Alpha3)) set_view(view_index, 2);
            if (Input.GetKeyDown(KeyCode.Tab)) set_view((view_index + 1) % VIEW_NAMES.Length, zoom_index);
            if (Input.GetKeyDown(KeyCode.B) && config.biomes != null && config.biomes.Length > 0) set_biome((biome_index + 1) % config.biomes.Length);
            if (Input.GetKeyDown(KeyCode.G)) { foliage.show_grass = !foliage.show_grass; reset_frame_samples(); }
            if (Input.GetKeyDown(KeyCode.H)) show_hud = !show_hud;
            float scroll = Input.mouseScrollDelta.y;
            if (scroll != 0) set_view(view_index, zoom_index + (scroll > 0 ? -1 : 1));
            record_frame_sample();
        }

        /// <summary>Submit once per camera render, including manual capture renders; never also submit from LateUpdate.</summary>
        private void submit_foliage(Camera camera)
        {
            if (camera == review_camera && enabled && foliage != null) foliage.draw_batches(camera);
        }

        public void reset_frame_samples()
        {
            frame_cursor = 0;
            frame_count = 0;
            median_ms = 0;
            p95_ms = 0;
            next_stats_time = Time.unscaledTime + 1;
        }

        private void record_frame_sample()
        {
            frame_samples[frame_cursor] = Time.unscaledDeltaTime * 1000;
            frame_cursor = (frame_cursor + 1) % frame_samples.Length;
            frame_count = Mathf.Min(frame_count + 1, frame_samples.Length);
            if (Time.unscaledTime < next_stats_time || frame_count < 30) return;
            var sorted = new float[frame_count];
            Array.Copy(frame_samples, sorted, frame_count);
            Array.Sort(sorted);
            median_ms = sorted[frame_count / 2];
            p95_ms = sorted[Mathf.Clamp(Mathf.CeilToInt(frame_count * .95f) - 1, 0, frame_count - 1)];
            next_stats_time = Time.unscaledTime + 1;
        }

        private void OnGUI()
        {
            if (!show_hud || ground == null || foliage == null) return;
            GUILayout.BeginArea(new Rect(18, 18, 530, 330), GUI.skin.box);
            // Built-in ASCII HUD: desktop system fonts are not reliably available in a packaged player.
            GUILayout.Label("ENVIRONMENT ART LAB - Six PBR layers - " + config.map_size + " m");
            GUILayout.Label("B: region | 1/2/3 or wheel: near/mid/far | Tab: view | G: grass | H: HUD");
            GUILayout.BeginHorizontal();
            if (config.biomes != null)
                for (int index = 0; index < config.biomes.Length; index++)
                    if (GUILayout.Button((biome_index == index ? "* " : "") + config.biomes[index].label)) set_biome(index);
            GUILayout.EndHorizontal();
            if (current_biome != null) GUILayout.Label(current_biome.label + " | " + current_biome.missing_art);
            GUILayout.Label(VIEW_NAMES[view_index] + " - " + new[] { "Near", "Middle", "Far" }[zoom_index]);
            GUILayout.BeginHorizontal();
            for (int index = 0; index < 3; index++)
                if (GUILayout.Button(new[] { "Near", "Middle", "Far" }[index])) set_view(view_index, index);
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Next view")) set_view((view_index + 1) % VIEW_NAMES.Length, zoom_index);
            if (GUILayout.Button(foliage.show_grass ? "Grass ON" : "Grass OFF")) { foliage.show_grass = !foliage.show_grass; reset_frame_samples(); }
            GUILayout.EndHorizontal();
            GUILayout.Label("Grass " + foliage.grass_instances + " | trees " + foliage.tree_instances + " | shrubs " + foliage.shrub_instances +
                " | rocks " + foliage.rock_instances + " | batches " + foliage.visible_batches);
            if (foliage.grass_instances == 0) GUILayout.Label("No grass in this region. Snow/sand are explicit ground sources.");
            GUILayout.Label("Frame median " + median_ms.ToString("F2") + " ms | p95 " + p95_ms.ToString("F2") + " ms | " + frame_count + " frames");
            GUILayout.Label(Screen.width + "×" + Screen.height + " · " + SystemInfo.graphicsDeviceName);
            GUILayout.Label("Art-only timing; not a battle benchmark. Height blending/Graph pending.");
            GUILayout.EndArea();
        }

        private void OnDestroy()
        {
            Camera.onPreCull -= submit_foliage;
            foliage?.Dispose();
            ground?.Dispose();
            if (water_material) Destroy(water_material);
            QualitySettings.vSyncCount = previous_vsync;
            Application.targetFrameRate = previous_target_framerate;
            Application.runInBackground = previous_run_in_background;
            QualitySettings.shadowDistance = previous_shadow_distance;
            QualitySettings.shadowCascades = previous_shadow_cascades;
        }
    }
}
