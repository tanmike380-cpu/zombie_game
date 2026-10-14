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
        public ShowcaseLandmarks landmarks { get; private set; }
        public ShowcaseConceptWater concept_water { get; private set; }
        public string[] view_names => config.concept_enabled ? new[] {"Concept 05 - Rounded grass slopes","Concept 07 - Limestone seacoast","Concept 09 - Closed natural pond","Concept composite overview"} : VIEW_NAMES;
        public Camera review_camera { get; private set; }
        public Renderer water_renderer => water_transform ? water_transform.GetComponent<Renderer>() : null;
        public bool show_hud = true;
        public int zoom_index { get; private set; } = 1;
        public int view_index { get; private set; }
        public int biome_index { get; private set; }
        public EnvironmentShowcaseConfig.BiomeProfile current_biome => config.biomes != null && config.biomes.Length > 0 ? config.biomes[biome_index] : null;
        public string missing_art => config.dark_enabled ? config.active_dark_layout.tree_source+(config.active_dark_layout.stone_candidate?.prefab ? " Licensed natural stone candidate present, not a Tripo stone mine; visual review pending." : " Stone source unavailable.")+" Environment props only; no economy." : config.concept_enabled ? config.active_concept_layout.missing_art : current_biome == null ? "" : ground != null && ground.routes.enabled ? current_biome.route_missing_art : current_biome.missing_art;
        private readonly float[] frame_samples = new float[600];
        private int frame_cursor;
        private int frame_count;
        private float median_ms;
        private float p95_ms;
        private float next_stats_time;
        private Material water_material;
        private Transform water_transform;
        private bool previous_route_layout;
        private int previous_vsync;
        private int previous_target_framerate;
        private bool previous_run_in_background;
        private float previous_shadow_distance;
        private int previous_shadow_cascades;
        private int previous_antialiasing;
        private Light sun;
        public static readonly string[] VIEW_NAMES = { "Meadow overview", "Road seam", "Wet shoreline", "Forest edge", "Actual rocky slope", "Cliff face", "Plateau top", "Talus foot",
            "River ford", "Continuous forest belt", "Neutral landmark", "Ore source detail", "Route panorama" };
        private static readonly Vector2[] VIEW_POINTS = { new Vector2(.5f, .53f), new Vector2(.42f, .48f),
            new Vector2(.43f, .235f), new Vector2(.61f, .57f), new Vector2(.70f, .72f) };

        private void Awake()
        {
            previous_vsync = QualitySettings.vSyncCount;
            previous_target_framerate = Application.targetFrameRate;
            previous_run_in_background = Application.runInBackground;
            previous_shadow_distance = QualitySettings.shadowDistance;
            previous_shadow_cascades = QualitySettings.shadowCascades;
            previous_antialiasing = QualitySettings.antiAliasing;
            Application.runInBackground = true;
            try
            {
                if (!config) throw new InvalidOperationException("Environment showcase requires its independent authored config asset.");
                previous_route_layout = config.route_layout != null && config.route_layout.enabled;
                config.concept_enabled = false;
                config.dark_enabled=false;
                config.flat_enabled=false;
                var arguments = Environment.GetCommandLineArgs();
                for (int index = 0; index + 1 < arguments.Length; index++)
                    if (arguments[index] == "-showcaseLayout")
                    {
                        string layout_name = arguments[index + 1];
                        if (layout_name != "cliffs" && layout_name != "routes" && layout_name != "concept" && layout_name!="dark" && layout_name!="flat") throw new InvalidOperationException("Use -showcaseLayout routes / cliffs / concept / dark / flat, got: " + layout_name);
                        config.flat_enabled=layout_name=="flat";
                        config.dark_enabled=layout_name=="dark"||config.flat_enabled;
                        if(config.dark_enabled && (!config.active_dark_layout||!config.active_dark_layout.layout)) throw new InvalidOperationException("Prepare independent wilderness preset before starting this layout.");
                        config.concept_enabled = layout_name == "concept" || config.dark_enabled;
                        if (!config.concept_enabled) config.route_layout.enabled = layout_name == "routes";
                    }
                config.validate_config();
                if(config.concept_enabled) QualitySettings.antiAliasing=4;
                configure_lighting();
                ground = new ShowcaseTerrain(config, transform, current_biome);
                foliage = new ShowcaseFoliage(config, ground);
                landmarks = new ShowcaseLandmarks(config,ground,transform);
                create_water();
                if (config.concept_enabled) concept_water = new ShowcaseConceptWater(ground,transform);
                create_camera();
                Camera.onPreCull += submit_foliage;
                set_view(ground.routes.enabled ? 10 : 0, 1);
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
            RenderSettings.ambientLight = config.dark_enabled ? config.active_dark_layout.ambient : current_biome == null ? config.ambient_color : current_biome.ambient;
            if(config.flat_enabled)
            {
                RenderSettings.ambientMode = config.active_dark_layout.ambient_mode;
                RenderSettings.ambientSkyColor = config.active_dark_layout.ambient_sky;
                RenderSettings.ambientEquatorColor = config.active_dark_layout.ambient_equator;
                RenderSettings.ambientGroundColor = config.active_dark_layout.ambient_ground;
            }
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = config.dark_enabled ? config.active_dark_layout.fog : current_biome == null ? config.fog_color : current_biome.fog;
            RenderSettings.fogStartDistance = config.fog_start;
            RenderSettings.fogEndDistance = config.fog_end;
            if (!sun)
            {
                var sun_object = new GameObject("Showcase afternoon sun");
                sun_object.transform.SetParent(transform, false);
                sun_object.transform.rotation = Quaternion.Euler(config.concept_enabled ? config.active_concept_layout.sun_rotation : config.sun_rotation);
                sun = sun_object.AddComponent<Light>();
            }
            sun.type = LightType.Directional;
            sun.color = config.dark_enabled ? config.active_dark_layout.sun : current_biome == null ? config.sun_color : current_biome.sun;
            sun.intensity = config.dark_enabled ? config.active_dark_layout.sun_intensity : current_biome == null ? config.sun_intensity : current_biome.sun_intensity;
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
            water_transform = water.transform;
            update_water_extent();
            Destroy(water.GetComponent<Collider>());
            var shader = Shader.Find("Standard");
            if (!shader) throw new InvalidOperationException("Built-in Standard shader missing for basic showcase water.");
            water_material = new Material(shader) { name = "Showcase basic opaque water", color = current_biome == null ? config.water_color : current_biome.water };
            water_material.SetFloat("_Metallic", 0);
            water_material.SetFloat("_Glossiness", .72f);
            water.GetComponent<Renderer>().sharedMaterial = water_material;
        }

        private void update_water_extent()
        {
            if (config.concept_enabled) { water_transform.gameObject.SetActive(false); return; }
            bool routes_enabled = config.route_layout != null && config.route_layout.enabled;
            water_transform.position = new Vector3(0,config.water_level,routes_enabled ? 0 : -config.map_size * .38f);
            water_transform.localScale = new Vector3(config.map_size / 10,1,config.map_size * (routes_enabled ? 1 : .30f) / 10);
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
            view_index = Mathf.Clamp(requested_view, 0, view_names.Length - 1);
            if (config.concept_enabled)
            {
                set_focus(config.active_concept_layout.views[view_index],requested_zoom);
                if(view_index==3) review_camera.orthographicSize *= 1.45f;
                return;
            }
            zoom_index = Mathf.Clamp(requested_zoom, 0, config.camera_sizes.Length - 1);
            var view_point = view_index < VIEW_POINTS.Length ? VIEW_POINTS[view_index] : new Vector2(.77f, .81f);
            var profile = current_biome;
            if (view_index >= VIEW_POINTS.Length && view_index <= 7 && profile != null)
            {
                view_point = profile.plateau_center;
                if (view_index != 6)
                    view_point.y -= (profile.plateau_radius.y + (view_index == 7 ? profile.talus_width * .45f +
                        (profile.dry_basin ? profile.cliff_edge_width * 2 : 0) : -profile.cliff_edge_width * .5f)) / config.map_size;
            }
            if (view_index >= 8 && ground.routes.enabled)
                view_point = view_index == 8 ? config.route_layout.ford : view_index == 9 ? new Vector2(.58f,.47f) :
                    view_index == 10 ? config.route_layout.landmarks[0] : view_index == 11 ? config.route_layout.ores[0] : new Vector2(.5f,.58f);
            set_focus(view_point,zoom_index);
        }

        public void set_focus(Vector2 view_point, int requested_zoom)
        {
            zoom_index = Mathf.Clamp(requested_zoom,0,config.camera_sizes.Length - 1);
            Vector3 focus = ground.sample_ground(view_point.x * config.map_size, view_point.y * config.map_size);
            Quaternion rotation = Quaternion.Euler(config.concept_enabled ? config.active_concept_layout.camera_pitch : config.camera_pitch,
                config.concept_enabled ? config.active_concept_layout.camera_yaw : config.camera_yaw, 0);
            float camera_size=config.concept_enabled ? config.active_concept_layout.camera_sizes[zoom_index] : config.camera_sizes[zoom_index];
            if(config.concept_enabled && view_index==2) camera_size*=.82f;
            review_camera.transform.SetPositionAndRotation(focus - rotation * Vector3.forward * camera_size * 2.3f, rotation);
            review_camera.orthographicSize = camera_size;
            reset_frame_samples();
        }

        /// <summary>Switch authored region in the same review controller; dispose only isolated runtime resources.</summary>
        public void set_biome(int requested_biome)
        {
            if (config.biomes == null || config.biomes.Length == 0) return;
            biome_index = Mathf.Clamp(requested_biome, 0, config.biomes.Length - 1);
            bool grass_enabled = foliage == null || foliage.show_grass;
            foliage?.Dispose();
            landmarks?.Dispose();
            ground?.Dispose();
            concept_water?.Dispose();
            ground = new ShowcaseTerrain(config, transform, current_biome);
            foliage = new ShowcaseFoliage(config, ground) { show_grass = grass_enabled };
            landmarks = new ShowcaseLandmarks(config,ground,transform);
            if(config.concept_enabled) concept_water = new ShowcaseConceptWater(ground,transform);
            update_water_extent();
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
            if (Input.GetKeyDown(KeyCode.Tab)) set_view((view_index + 1) % view_names.Length, zoom_index);
            if (Input.GetKeyDown(KeyCode.B) && !config.concept_enabled && config.biomes != null && config.biomes.Length > 0) set_biome((biome_index + 1) % config.biomes.Length);
            if (Input.GetKeyDown(KeyCode.G)) { foliage.show_grass = !foliage.show_grass; reset_frame_samples(); }
            if (Input.GetKeyDown(KeyCode.H)) show_hud = !show_hud;
            if (Input.GetKeyDown(KeyCode.L) && !config.concept_enabled)
            {
                config.route_layout.enabled = !config.route_layout.enabled;
                set_biome(biome_index);
            }
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
            GUILayout.Label(config.concept_enabled ? "1/2/3: zoom | Tab: 05 slopes / 07 coast / 09 pond / overview | G: grass | H: HUD" : "B: region | 1/2/3: zoom | Tab: view | G: grass | H: HUD | L: layout");
            GUILayout.Label(config.flat_enabled ? "FLAT WILDERNESS - level land, no roads/rivers; visual review pending" : config.dark_enabled ? "DARK WILDERNESS - source buildings and ore displays; art review pending" : config.concept_enabled ? "WILDERNESS PREVIEW - no roads/rivers; visual review pending" : ground.routes.enabled ? "ROUTES: river / forest belts / neutral landmarks (art only)" : "CLIFFS: preserved previous layout baseline");
            GUILayout.BeginHorizontal();
            if (!config.concept_enabled && config.biomes != null)
                for (int index = 0; index < config.biomes.Length; index++)
                    if (GUILayout.Button((biome_index == index ? "* " : "") + config.biomes[index].label)) set_biome(index);
            GUILayout.EndHorizontal();
            if (current_biome != null) GUILayout.Label(current_biome.label + " | " + missing_art);
            GUILayout.Label(view_names[view_index] + " - " + new[] { "Near", "Middle", "Far" }[zoom_index]);
            GUILayout.BeginHorizontal();
            for (int index = 0; index < 3; index++)
                if (GUILayout.Button(new[] { "Near", "Middle", "Far" }[index])) set_view(view_index, index);
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Next view")) set_view((view_index + 1) % view_names.Length, zoom_index);
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
            concept_water?.Dispose();
            if(config) config.concept_enabled=false;
            if(config) config.dark_enabled=false;
            if(config) config.flat_enabled=false;
            Camera.onPreCull -= submit_foliage;
            foliage?.Dispose();
            landmarks?.Dispose();
            ground?.Dispose();
            if (water_material) Destroy(water_material);
            QualitySettings.vSyncCount = previous_vsync;
            Application.targetFrameRate = previous_target_framerate;
            Application.runInBackground = previous_run_in_background;
            QualitySettings.shadowDistance = previous_shadow_distance;
            QualitySettings.shadowCascades = previous_shadow_cascades;
            QualitySettings.antiAliasing = previous_antialiasing;
            if (config && config.route_layout != null) config.route_layout.enabled = previous_route_layout;
        }
    }
}
