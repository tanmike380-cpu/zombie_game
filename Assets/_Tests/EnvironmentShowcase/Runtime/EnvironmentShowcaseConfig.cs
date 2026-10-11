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
            public string id, label, source_record, missing_art;
            public string[] layer_semantics;
            public TerrainLayer[] layer_overrides = new TerrainLayer[6];
            public float relief_multiplier = 1, rock_start = 16, rock_full = 34, dry_strength = .3f;
            public float grass_density = 1, tree_density = 1, shrub_density = 1, rock_density = 1;
            public bool upper_snow, dry_basin;
            public Color[] ground_tints = { Color.white, Color.white, Color.white, Color.white };
            public Color ambient = new Color(.34f, .39f, .34f), sun = new Color(1, .94f, .84f);
            public Color fog = new Color(.40f, .49f, .50f), water = new Color(.16f, .31f, .33f);
            public float sun_intensity = 1;
        }

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
            if (biomes != null)
                foreach (var biome in biomes)
                    if (biome == null || string.IsNullOrWhiteSpace(biome.id) || string.IsNullOrWhiteSpace(biome.source_record) ||
                        biome.layer_overrides == null || biome.layer_overrides.Length != 6 || biome.layer_semantics == null || biome.layer_semantics.Length != 6 ||
                        biome.ground_tints == null || biome.ground_tints.Length != 4 || biome.relief_multiplier <= 0 ||
                        hill_height * biome.relief_multiplier + 3 >= height_range || biome.rock_full <= biome.rock_start ||
                        biome.grass_density < 0 || biome.tree_density < 0 || biome.shrub_density < 0 || biome.rock_density < 0)
                        throw new InvalidOperationException("Showcase biome provenance, six semantics or relief bounds invalid: " + biome?.id);
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
