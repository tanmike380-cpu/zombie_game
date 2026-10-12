using System;
using UnityEngine;

namespace ZombieGame.Tests.EnvironmentShowcase
{
    /// <summary>Independent art-review parameters; never used by battle or navigation.</summary>
    [CreateAssetMenu(menuName = "Zombie Game/Environment showcase config")]
    public sealed class EnvironmentShowcaseConfig : ScriptableObject
    {
        [Serializable]
        public sealed class BiomeProfile
        {
            public string id, label, source_record, missing_art, route_missing_art;
            public string[] layer_semantics;
            public TerrainLayer[] layer_overrides = new TerrainLayer[6];
            public float relief_multiplier = 1, rock_start = 16, rock_full = 34, dry_strength = .3f;
            public float grass_density = 1, tree_density = 1, shrub_density = 1, rock_density = 1;
            public bool upper_snow, dry_basin;
            public Color[] ground_tints = { Color.white, Color.white, Color.white, Color.white };
            public Color ambient = new Color(.34f, .39f, .34f), sun = new Color(1, .94f, .84f);
            public Color fog = new Color(.40f, .49f, .50f), water = new Color(.16f, .31f, .33f);
            public float sun_intensity = 1;
            public bool plateau_enabled = true;
            public Vector2 plateau_center = new Vector2(.77f, .81f);
            public Vector2 plateau_radius = new Vector2(17, 11);
            public float plateau_height = 11, cliff_edge_width = 2.2f, talus_width = 6;
            public float erosion_strength = 0, talus_rock_fraction = .55f;
            public Texture2D cliff_albedo_smoothness, cliff_normal, talus_albedo_smoothness, talus_normal;
            public string cliff_source_record, talus_source_record;
            public float cliff_tile_size = 4, talus_tile_size = 2.5f, cliff_normal_strength = .3f;
            public int[] landmark_source_indices = { 0,0,0 };
        }

        [Serializable] public sealed class SourceAsset
        {
            public string id, source_record;
            public GameObject prefab;
            public Vector3 native_dimensions;
            public Vector3 native_bounds_center;
            public SourceMaterialBinding[] material_bindings;
        }

        [Serializable] public sealed class SourceMaterialBinding
        {
            public Material material;
            public Texture2D base_color, normal, roughness, metallic;
            public bool import_channels_pass;
            public float metallic_max_red, metallic_multiplier;
            public string normal_note;
        }

        [Serializable] public sealed class RoutePath
        {
            public Vector2[] points;
        }

        [Serializable] public sealed class RouteLayout
        {
            public bool enabled;
            public string version = "routes-v1; previous baseline cliffs-final";
            public Vector2[] river = { new Vector2(.23f,.16f), new Vector2(.25f,.28f), new Vector2(.30f,.39f),
                new Vector2(.26f,.55f), new Vector2(.32f,.70f), new Vector2(.23f,.88f) };
            public float river_half_width = 2.5f, river_bank_width = 5.5f, river_depth = .5f;
            public Vector2 ford = new Vector2(.26f,.55f);
            public float ford_radius = 5, ford_depth = .12f;
            public RoutePath[] roads = {
                new RoutePath { points = new[] { new Vector2(.45f,.25f), new Vector2(.44f,.42f), new Vector2(.42f,.55f), new Vector2(.43f,.68f), new Vector2(.40f,.84f) } },
                new RoutePath { points = new[] { new Vector2(.42f,.55f), new Vector2(.34f,.55f), new Vector2(.26f,.55f), new Vector2(.17f,.55f), new Vector2(.15f,.68f) } },
                new RoutePath { points = new[] { new Vector2(.44f,.42f), new Vector2(.55f,.43f), new Vector2(.65f,.49f), new Vector2(.78f,.50f), new Vector2(.91f,.50f), new Vector2(.93f,.62f) } } };
            public RoutePath[] forest_bands = {
                new RoutePath { points = new[] { new Vector2(.52f,.34f), new Vector2(.57f,.40f), new Vector2(.58f,.51f), new Vector2(.61f,.57f) } },
                new RoutePath { points = new[] { new Vector2(.52f,.64f), new Vector2(.55f,.72f), new Vector2(.62f,.78f) } } };
            public float forest_half_width = 6, tree_spacing = 2.55f;
            public int tree_budget = 200;
            public float corridor_half_width = 3.5f, road_height = 2.5f;
            public Vector2 highland_center = new Vector2(.12f,.75f), highland_radius = new Vector2(10,15);
            public float highland_height = 7, highland_edge_width = 3;
            public Vector2[] landmarks = { new Vector2(.49f,.31f), new Vector2(.47f,.83f), new Vector2(.86f,.58f) };
            public Vector2[] ores = { new Vector2(.37f,.35f), new Vector2(.38f,.66f), new Vector2(.71f,.40f),
                new Vector2(.48f,.60f), new Vector2(.82f,.42f), new Vector2(.68f,.83f) };
            public float ore_uniform_scale = 2.5f;
            public float landmark_clear_radius = 7, landmark_pad_radius = 5.7f;
        }

        public RouteLayout route_layout = new RouteLayout();
        public ConceptLayoutConfig concept_layout;
        [NonSerialized] public bool concept_enabled;
        public SourceAsset[] landmark_sources = Array.Empty<SourceAsset>();
        public SourceAsset[] ore_sources = Array.Empty<SourceAsset>();

        [Header("Independent region profiles; six source slots remain explicit")]
        public BiomeProfile[] biomes = Array.Empty<BiomeProfile>();
        [Header("Ground: metres, not unit balance")]
        public int seed = 173;
        public float map_size = 128;
        public int heightmap_resolution = 257;
        public int alphamap_resolution = 512;
        public float height_range = 18;
        public float hill_height = 11;
        public float macro_patch_size = 24;
        public float slope_rock_start = 16;
        public float slope_rock_full = 34;
        public float road_half_width = 1.35f;
        public float road_edge_width = 1.5f;
        public float shore_blend_width = 5;
        public float water_level = 1.1f;
        public float texture_scale_multiplier = 1;
        public float dry_grass_strength = .38f;
        public TerrainLayer grass_layer_override;
        public Material terrain_material;
        public string ground_source_record = "";
        public float macro_color_strength = .22f;
        public float neighbor_breakup_strength = .52f;
        public Color grass_tint = new Color(.78f, .95f, .69f);
        public Color dry_tint = new Color(.81f, .81f, .62f);
        public Color soil_tint = new Color(.86f, .82f, .73f);
        public Color mud_tint = new Color(.76f, .83f, .74f);
        public float detail_normal_fade_start = 28;
        public float detail_normal_fade_end = 100;

        [Header("Presentation: Built-in, no pipeline migration")]
        public Color ambient_color = new Color(.43f, .48f, .49f);
        public Color sun_color = new Color(1, .9f, .73f);
        public float sun_intensity = 1.15f;
        public Vector3 sun_rotation = new Vector3(48, -35, 0);
        public Color fog_color = new Color(.40f, .49f, .50f);
        public float fog_start = 100;
        public float fog_end = 260;
        public Color water_color = new Color(.16f, .31f, .33f);
        public float camera_pitch = 48;
        public float camera_yaw = 0;
        public float[] camera_sizes = { 13, 29, 59 };
        public int target_frame_rate = 60;
        public float shadow_distance = 45;
        public int shadow_cascades = 1;
        public float tree_shadow_draw_distance = 45;

        [Header("Approved source assets only; empty means visibly unavailable")]
        public GameObject grass_prefab;
        public GameObject[] grass_variants = Array.Empty<GameObject>();
        public GameObject[] tree_prefabs = Array.Empty<GameObject>();
        public GameObject[] shrub_prefabs = Array.Empty<GameObject>();
        public GameObject[] rock_prefabs = Array.Empty<GameObject>();
        public string grass_source_record = "";
        public string tree_source_record = "";
        public string shrub_source_record = "";
        public string rock_source_record = "";
        public int grass_count = 10000;
        public Vector2 grass_focus_center = new Vector2(.53f, .55f);
        public Vector2 grass_focus_extent = new Vector2(.16f, .17f);
        [Range(0, 1)] public float grass_focus_fraction = .72f;
        public int tree_count = 115;
        public int shrub_count = 140;
        public int rock_count = 24;
        public float grass_draw_distance = 58;
        public float tree_draw_distance = 180;
        public float shrub_draw_distance = 90;
        public float rock_draw_distance = 140;
        public float grass_chunk_size = 12;
        public Vector2 grass_scale_range = new Vector2(.85f, 1.2f);
        public Vector2 tree_scale_range = new Vector2(.85f, 1.1f);
        public Vector2 shrub_scale_range = new Vector2(.85f, 1.1f);
        public Vector2 rock_scale_range = new Vector2(.8f, 1.2f);

        public void validate_config()
        {
            if (concept_enabled)
            {
                if (!concept_layout) throw new InvalidOperationException("Prepare approved concept config before concept mode.");
                concept_layout.validate_config(map_size,height_range);
            }
            if (map_size < 64 || map_size > 256 || height_range <= hill_height + 3 || hill_height <= 0)
                throw new InvalidOperationException("Showcase ground needs 64–256 m and height_range > hill_height + 3 m base relief.");
            int intervals = heightmap_resolution - 1;
            if (intervals < 32 || (intervals & (intervals - 1)) != 0 || alphamap_resolution < 128)
                throw new InvalidOperationException("Showcase heightmap resolution must be 2^n + 1, alphamap >= 128.");
            if (macro_patch_size <= 0 || road_edge_width <= 0 || shore_blend_width <= 0 ||
                slope_rock_full <= slope_rock_start || texture_scale_multiplier <= 0 || dry_grass_strength < 0 || dry_grass_strength > 1 ||
                detail_normal_fade_end <= detail_normal_fade_start || grass_focus_extent.x <= 0 || grass_focus_extent.y <= 0)
                throw new InvalidOperationException("Showcase blend scales and slope thresholds are invalid.");
            if (camera_sizes == null || camera_sizes.Length != 3 || camera_sizes[0] <= 0 ||
                camera_sizes[1] <= camera_sizes[0] || camera_sizes[2] <= camera_sizes[1])
                throw new InvalidOperationException("Showcase requires increasing close / middle / far camera sizes.");
            if (target_frame_rate < 30 || shadow_distance <= 0 || tree_shadow_draw_distance <= 0 ||
                (shadow_cascades != 1 && shadow_cascades != 2 && shadow_cascades != 4))
                throw new InvalidOperationException("Showcase frame cap and runtime shadow budget are invalid.");
            if (grass_count < 0 || tree_count < 0 || shrub_count < 0 || rock_count < 0 || grass_chunk_size <= 0 || grass_draw_distance <= 0 ||
                tree_draw_distance <= 0 || shrub_draw_distance <= 0 || rock_draw_distance <= 0 || grass_scale_range.x <= 0 || grass_scale_range.y < grass_scale_range.x ||
                tree_scale_range.x <= 0 || tree_scale_range.y < tree_scale_range.x || shrub_scale_range.x <= 0 || shrub_scale_range.y < shrub_scale_range.x ||
                rock_scale_range.x <= 0 || rock_scale_range.y < rock_scale_range.x)
                throw new InvalidOperationException("Showcase decoration budgets and chunk sizes are invalid.");
            validate_source(grass_prefab, grass_source_record, "grass");
            validate_sources(grass_variants, grass_source_record, "grass");
            validate_sources(tree_prefabs, tree_source_record, "tree");
            validate_sources(shrub_prefabs, shrub_source_record, "shrub");
            validate_sources(rock_prefabs, rock_source_record, "rock");
            if (route_layout != null && route_layout.enabled) validate_routes();
            if (biomes != null)
            {
                foreach (var biome in biomes)
                {
                    if (biome == null || string.IsNullOrWhiteSpace(biome.id) || string.IsNullOrWhiteSpace(biome.source_record) ||
                        biome.layer_overrides == null || biome.layer_overrides.Length != 6 || biome.layer_semantics == null || biome.layer_semantics.Length != 6 ||
                        biome.ground_tints == null || biome.ground_tints.Length != 4 || biome.relief_multiplier <= 0 ||
                        hill_height * biome.relief_multiplier + 3 >= height_range || biome.rock_full <= biome.rock_start ||
                        biome.grass_density < 0 || biome.tree_density < 0 || biome.shrub_density < 0 || biome.rock_density < 0)
                        throw new InvalidOperationException("Showcase biome provenance, six semantics or relief bounds invalid: " + biome?.id);
                    else validate_plateau(biome);
                    if (biome.landmark_source_indices == null || biome.landmark_source_indices.Length != 3)
                        throw new InvalidOperationException("Biome requires three landmark source selectors: " + biome.id);
                    foreach (int source_index in biome.landmark_source_indices)
                        if (source_index < 0 || source_index >= 3) throw new InvalidOperationException("Biome landmark index invalid: " + biome.id);
                }
            }
        }

        private void validate_plateau(BiomeProfile biome)
        {
            if (!biome.plateau_enabled) return;
            if (biome.plateau_height <= 4 || biome.plateau_height >= height_range - .5f || biome.cliff_edge_width < 1 ||
                biome.talus_width < 2 || biome.plateau_radius.x < 8 || biome.plateau_radius.y < 8 ||
                biome.cliff_tile_size <= 0 || biome.talus_tile_size <= 0 || biome.erosion_strength < 0 || biome.erosion_strength > .15f ||
                biome.talus_rock_fraction < 0 || biome.talus_rock_fraction > .8f || biome.plateau_center.x < .7f || biome.plateau_center.y < .8f ||
                biome.plateau_center.x * map_size + biome.plateau_radius.x * (1 + biome.erosion_strength * 1.4f) + biome.cliff_edge_width * 2 + biome.talus_width > map_size ||
                biome.plateau_center.y * map_size + biome.plateau_radius.y * (1 + biome.erosion_strength * 1.4f) + biome.cliff_edge_width * 2 + biome.talus_width > map_size)
                throw new InvalidOperationException("Showcase plateau bounds/height/projection invalid: " + biome.id);
            if ((biome.cliff_albedo_smoothness || biome.cliff_normal) &&
                (!biome.cliff_albedo_smoothness || !biome.cliff_normal || string.IsNullOrWhiteSpace(biome.cliff_source_record)))
                throw new InvalidOperationException("Cliff PBR requires paired approved base+normal and source record: " + biome.id);
            if ((biome.talus_albedo_smoothness || biome.talus_normal) &&
                (!biome.talus_albedo_smoothness || !biome.talus_normal || string.IsNullOrWhiteSpace(biome.talus_source_record)))
                throw new InvalidOperationException("Talus PBR requires paired approved base+normal and source record: " + biome.id);
        }

        private void validate_routes()
        {
            var layout = route_layout;
            if (layout.river_half_width < 2 || layout.river_bank_width < 3 || layout.river_depth <= 0 ||
                layout.river_depth >= water_level || layout.ford_depth <= 0 || layout.ford_depth >= layout.river_depth ||
                layout.tree_spacing < 2 || layout.tree_budget < 80 || layout.corridor_half_width < 3 ||
                layout.ore_uniform_scale < 1 || layout.ore_uniform_scale > 4 ||
                layout.highland_height <= 0 || layout.highland_height >= height_range - .5f || layout.highland_edge_width < 1 ||
                layout.highland_center.x * map_size < layout.highland_radius.x || layout.highland_center.y * map_size < layout.highland_radius.y ||
                layout.highland_center.x * map_size + layout.highland_radius.x > map_size || layout.highland_center.y * map_size + layout.highland_radius.y > map_size ||
                layout.landmarks.Length != 3 || layout.ores.Length != 6 || landmark_sources.Length != 3 || ore_sources.Length != 2)
                throw new InvalidOperationException("Route layout widths/budgets or approved source counts invalid.");
            foreach (var source in landmark_sources) validate_route_source(source);
            foreach (var source in ore_sources) validate_route_source(source);
            validate_points(layout.river);
            validate_points(layout.landmarks);
            validate_points(layout.ores);
            foreach (var path in layout.roads) validate_points(path.points);
            foreach (var path in layout.forest_bands) validate_points(path.points);
        }

        private static void validate_points(Vector2[] points)
        {
            if (points == null || points.Length < 2) throw new InvalidOperationException("Route requires at least two authored points.");
            foreach (var point in points)
                if (float.IsNaN(point.x) || float.IsNaN(point.y) || point.x < 0 || point.y < 0 || point.x > 1 || point.y > 1)
                    throw new InvalidOperationException("Route authored point lies outside terrain: " + point);
        }

        private static void validate_route_source(SourceAsset source)
        {
            if (source == null || !source.prefab || string.IsNullOrWhiteSpace(source.source_record) ||
                source.native_dimensions.x <= 0 || source.native_dimensions.y <= 0 || source.native_dimensions.z <= 0)
                throw new InvalidOperationException("Route fixture requires approved prefab/provenance/imported vertex bounds: " + source?.id);
        }

        private static void validate_source(GameObject prefab, string source_record, string category)
        {
            if (prefab && string.IsNullOrWhiteSpace(source_record))
                throw new InvalidOperationException("Showcase " + category + " requires a traceable approved source record.");
        }

        private static void validate_sources(GameObject[] prefabs, string source_record, string category)
        {
            if (prefabs == null) return;
            foreach (var prefab in prefabs) validate_source(prefab, source_record, category);
        }
    }
}
