using System;
using UnityEngine;
using ZombieGame.World;

namespace ZombieGame.Tests.EnvironmentShowcase
{
    /// <summary>Actual isolated relief, road and shore semantics. No walkability claims.</summary>
    public sealed class ShowcaseTerrain : IDisposable
    {
        private readonly EnvironmentShowcaseConfig config;
        public EnvironmentShowcaseConfig.BiomeProfile biome { get; private set; }
        private readonly TerrainLayer[] layer_instances;
        private Material material_instance;
        public Terrain terrain { get; private set; }
        public TerrainData terrain_data { get; private set; }
        public ShowcaseRoutes routes { get; private set; }
        public Vector3 origin => new Vector3(-config.map_size * .5f, 0, -config.map_size * .5f);

        public ShowcaseTerrain(EnvironmentShowcaseConfig settings, Transform parent, EnvironmentShowcaseConfig.BiomeProfile profile = null)
        {
            config = settings;
            biome = profile;
            routes = new ShowcaseRoutes(settings);
            layer_instances = load_layers();
            terrain_data = new TerrainData { name = "Showcase real relief", heightmapResolution = config.heightmap_resolution,
                alphamapResolution = config.alphamap_resolution, baseMapResolution = 1024,
                size = new Vector3(config.map_size, config.height_range, config.map_size) };
            terrain_data.terrainLayers = layer_instances;
            terrain_data.SetHeights(0, 0, build_heights());
            terrain_data.SetAlphamaps(0, 0, build_weights());
            var terrain_object = Terrain.CreateTerrainGameObject(terrain_data);
            terrain_object.name = "Six PBR layers — isolated real hills";
            terrain_object.transform.SetParent(parent, false);
            terrain_object.transform.position = origin;
            terrain = terrain_object.GetComponent<Terrain>();
            var material_source = config.terrain_material ? config.terrain_material : Resources.Load<Material>(TerrainPresentation.GENERATED_ROOT + "TerrainStandard");
            if (!material_source) throw new InvalidOperationException("Prepare TerrainLayerImport six PBR layers before creating showcase.");
            material_instance = UnityEngine.Object.Instantiate(material_source);
            configure_material();
            terrain.materialTemplate = material_instance;
            terrain.drawInstanced = true;
            terrain.heightmapPixelError = 3;
            terrain.basemapDistance = 400;
            terrain.Flush();
        }

        private TerrainLayer[] load_layers()
        {
            var profile = TerrainPresentation.load_config();
            var result = new TerrainLayer[profile.layers.Length];
            for (int layer_index = 0; layer_index < result.Length; layer_index++)
            {
                string resource_path = TerrainPresentation.GENERATED_ROOT + profile.layers[layer_index].id;
                var source = layer_index == 0 && config.grass_layer_override ? config.grass_layer_override : Resources.Load<TerrainLayer>(resource_path);
                if (biome != null && biome.layer_overrides[layer_index]) source = biome.layer_overrides[layer_index];
                if (!source || !source.diffuseTexture || !source.normalMapTexture)
                    throw new InvalidOperationException("Missing prepared PBR terrain layer: " + resource_path);
                result[layer_index] = UnityEngine.Object.Instantiate(source);
                result[layer_index].tileSize *= config.texture_scale_multiplier;
                result[layer_index].tileOffset = new Vector2(layer_index * 1.31f, layer_index * .73f);
            }
            return result;
        }

        private void configure_material()
        {
            if (!material_instance.HasProperty("_ShowcaseTerrainSize")) return;
            material_instance.SetFloat("_ShowcaseTerrainSize", config.map_size);
            material_instance.SetVector("_ShowcaseTerrainOrigin", origin);
            material_instance.SetFloat("_MacroScale", config.macro_patch_size);
            material_instance.SetFloat("_MacroStrength", config.macro_color_strength);
            material_instance.SetFloat("_BreakupStrength", config.neighbor_breakup_strength);
            material_instance.SetFloat("_DetailFadeStart", config.detail_normal_fade_start);
            material_instance.SetFloat("_DetailFadeEnd", config.detail_normal_fade_end);
            material_instance.SetColor("_GrassTint", biome == null ? config.grass_tint : biome.ground_tints[0]);
            material_instance.SetColor("_DryTint", biome == null ? config.dry_tint : biome.ground_tints[1]);
            material_instance.SetColor("_SoilTint", biome == null ? config.soil_tint : biome.ground_tints[2]);
            material_instance.SetColor("_MudTint", biome == null ? config.mud_tint : biome.ground_tints[3]);
            if (biome == null) return;
            material_instance.SetVector("_PlateauCenter", new Vector4(biome.plateau_center.x * config.map_size + origin.x, biome.plateau_center.y * config.map_size + origin.z, 0, 0));
            material_instance.SetVector("_PlateauRadius", new Vector4(biome.plateau_radius.x, biome.plateau_radius.y, biome.cliff_edge_width, biome.talus_width));
            material_instance.SetFloat("_PlateauEnabled", biome.plateau_enabled ? 1 : 0);
            material_instance.SetFloat("_PlateauLedge", biome.dry_basin ? 1 : 0);
            material_instance.SetFloat("_ErosionStrength", biome.erosion_strength);
            material_instance.SetFloat("_HasCliffPbr", biome.cliff_albedo_smoothness && biome.cliff_normal ? 1 : 0);
            material_instance.SetFloat("_HasTalusPbr", biome.talus_albedo_smoothness && biome.talus_normal ? 1 : 0);
            material_instance.SetFloat("_CliffTileSize", biome.cliff_tile_size);
            material_instance.SetFloat("_TalusTileSize", biome.talus_tile_size);
            material_instance.SetFloat("_CliffNormalStrength", biome.cliff_normal_strength);
            if (biome.cliff_albedo_smoothness) material_instance.SetTexture("_CliffPbr", biome.cliff_albedo_smoothness);
            if (biome.cliff_normal) material_instance.SetTexture("_CliffNormal", biome.cliff_normal);
            if (biome.talus_albedo_smoothness) material_instance.SetTexture("_TalusPbr", biome.talus_albedo_smoothness);
            if (biome.talus_normal) material_instance.SetTexture("_TalusNormal", biome.talus_normal);
        }

        private float[,] build_heights()
        {
            int resolution = config.heightmap_resolution;
            var result = new float[resolution, resolution];
            for (int z = 0; z < resolution; z++)
                for (int x = 0; x < resolution; x++)
                {
                    float world_x = x * config.map_size / (resolution - 1);
                    float world_z = z * config.map_size / (resolution - 1);
                    float height = sample_authored_height(world_x, world_z);
                    if (float.IsNaN(height) || float.IsInfinity(height) || height < 0 || height >= config.height_range)
                        throw new InvalidOperationException("Showcase height outside authored bounds before Terrain clamp: " + height + " at " + world_x + "," + world_z);
                    result[z, x] = height / config.height_range;
                }
            return result;
        }

        public float sample_authored_height(float x, float z)
        {
            float original = sample_original_height(x, z);
            float height = sample_cliff_height(x, z, original);
            return routes.enabled ? routes.sample_height(x,z,height,original) : height;
        }

        private float sample_cliff_height(float x, float z, float original)
        {
            if (biome == null || !biome.plateau_enabled) return original;
            float signed_distance = plateau_edge_distance(x, z);
            float interior = 1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(-biome.cliff_edge_width, 0, signed_distance));
            if (biome.dry_basin)
            {
                float ledge = 1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(biome.cliff_edge_width, biome.cliff_edge_width * 2, signed_distance));
                original = Mathf.Lerp(original, Mathf.Max(original, biome.plateau_height * .48f), ledge);
            }
            return Mathf.Lerp(original, biome.plateau_height, interior);
        }

        /// <summary>Previous accepted four-biome relief, kept unchanged outside the explicit NE landform.</summary>
        public float sample_original_height(float x, float z)
        {
            float size = config.map_size;
            float ridge = Mathf.Exp(-((x - size * .73f) * (x - size * .73f) / (size * size * .009f) +
                (z - size * .72f) * (z - size * .72f) / (size * size * .027f)));
            float rolling = Mathf.PerlinNoise(x / 34 + config.seed * .017f, z / 34) * 1.1f;
            float shore = Mathf.SmoothStep(0, 1, Mathf.Clamp01((z - shoreline_z(x)) / 8));
            float relief = biome == null ? 1 : biome.relief_multiplier;
            if (biome != null && biome.dry_basin)
                rolling += Mathf.Sin(x / 15 + z / 27) * .5f + .5f;
            return (1.8f + rolling + ridge * config.hill_height * relief) * shore;
        }

        public float plateau_edge_distance(float x, float z)
        {
            if (biome == null || !biome.plateau_enabled) return float.PositiveInfinity;
            var delta = new Vector2((x - biome.plateau_center.x * config.map_size) / biome.plateau_radius.x,
                (z - biome.plateau_center.y * config.map_size) / biome.plateau_radius.y);
            float angle = Mathf.Atan2(delta.y, delta.x);
            float eroded_radius = 1 + (Mathf.Sin(angle * 7) + Mathf.Sin(angle * 13) * .4f) * biome.erosion_strength;
            // Radial metres along this ellipse direction, not the shorter-axis scale.
            float physical_radius = Mathf.Max(Mathf.Min(biome.plateau_radius.x, biome.plateau_radius.y),
                Vector2.Scale(delta, biome.plateau_radius).magnitude / Mathf.Max(delta.magnitude, .0001f));
            return (delta.magnitude - eroded_radius) * physical_radius;
        }

        public float talus_weight(float x, float z)
        {
            if (biome == null || !biome.plateau_enabled) return 0;
            float distance = plateau_edge_distance(x, z) - (biome.dry_basin ? biome.cliff_edge_width * 2 : 0);
            return Mathf.SmoothStep(0, 1, Mathf.InverseLerp(-.5f, 1, distance)) *
                (1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(1, biome.talus_width, distance)));
        }

        public float shoreline_z(float x) => config.map_size * .18f + Mathf.Sin(x / config.map_size * 5.8f) * 5;
        public float road_x(float z) => config.map_size * .42f + Mathf.Sin(z / config.map_size * 6.2f) * 8;
        public float forest_weight(float x, float z)
        {
            if (routes.enabled) return routes.sample_forest(x,z);
            float border = config.map_size * .58f + Mathf.Sin(z / 17) * 7;
            return Mathf.SmoothStep(0, 1, Mathf.Clamp01((x - border) / 13)) *
                Mathf.SmoothStep(0, 1, Mathf.Clamp01((z - config.map_size * .32f) / 12));
        }

        private float[,,] build_weights()
        {
            int resolution = config.alphamap_resolution;
            var result = new float[resolution, resolution, 6];
            var weights = new float[6];
            for (int z = 0; z < resolution; z++)
                for (int x = 0; x < resolution; x++)
                {
                    float u = x / (float)(resolution - 1), v = z / (float)(resolution - 1);
                    sample_weights(u, v, weights);
                    for (int layer_index = 0; layer_index < 6; layer_index++) result[z, x, layer_index] = weights[layer_index];
                }
            return result;
        }

        /// <summary>Six normalized weights; soil/wetness/slope are spatially meaningful, not random splats.</summary>
        public void sample_weights(float u, float v, float[] weights)
        {
            float x = u * config.map_size, z = v * config.map_size;
            float patch = Mathf.PerlinNoise(x / config.macro_patch_size + config.seed * .011f, z / config.macro_patch_size);
            float dry = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.38f, .78f, patch));
            float slope = terrain_data.GetSteepness(u, v);
            float rock_start = biome == null ? config.slope_rock_start : biome.rock_start;
            float rock_full = biome == null ? config.slope_rock_full : biome.rock_full;
            float stone = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(rock_start, rock_full, slope));
            float shore_distance = z - shoreline_z(x);
            float wet = 1 - Mathf.SmoothStep(0, 1, Mathf.Clamp01(shore_distance / config.shore_blend_width));
            if (routes.enabled) wet = Mathf.Max(wet, 1 - Mathf.SmoothStep(0,1,Mathf.InverseLerp(routes.layout.river_half_width,
                routes.layout.river_half_width + 3, routes.measure_river(x,z))));
            float road_distance = routes.enabled ? routes.measure_road(x,z) : Mathf.Abs(x - road_x(z));
            float road = 1 - Mathf.SmoothStep(0, 1, Mathf.Clamp01((road_distance - config.road_half_width) / config.road_edge_width));
            road *= Mathf.SmoothStep(0, 1, Mathf.Clamp01((shore_distance - 5) / 7));
            float path_soil = 1 - Mathf.Clamp01((road_distance - config.road_half_width) / (config.road_edge_width * 2.4f));
            float dirt = Mathf.Clamp01(path_soil * .45f + forest_weight(x, z) * .36f);
            if (routes.enabled) dirt = Mathf.Max(dirt, (1 - Mathf.SmoothStep(0,1,Mathf.InverseLerp(routes.layout.landmark_pad_radius,
                routes.layout.landmark_clear_radius, routes.measure_courtyard(x,z)))) * .8f);
            float dry_strength = biome == null ? config.dry_grass_strength : biome.dry_strength;
            if (biome != null && biome.dry_basin) dirt = path_soil * .35f + stone * .1f;
            float talus = talus_weight(x, z);
            stone = Mathf.Max(stone, talus * .52f);
            dirt = Mathf.Clamp01(dirt + talus * .32f);
            float upper_snow = biome != null && biome.upper_snow ?
                Mathf.SmoothStep(0, 1, Mathf.InverseLerp(7, 13, terrain_data.GetInterpolatedHeight(u, v))) : 0;
            weights[0] = (1 - dry * dry_strength) * (1 - dirt) * (1 - upper_snow);
            weights[1] = (dry * dry_strength * (1 - dirt) * (1 - upper_snow)) + upper_snow * (1 - stone);
            weights[2] = dirt;
            weights[3] = wet * 2;
            weights[4] = stone * 3;
            weights[5] = road * 3;
            for (int i = 0; i < 3; i++) weights[i] *= (1 - wet) * (1 - stone) * (1 - road);
            weights[3] *= 1 - stone;
            weights[4] *= 1 - road;
            float total = 0;
            for (int i = 0; i < weights.Length; i++) total += weights[i];
            for (int i = 0; i < weights.Length; i++) weights[i] /= Mathf.Max(.0001f, total);
        }

        public Vector3 sample_ground(float x, float z)
        {
            var point = origin + new Vector3(x, 0, z);
            point.y = terrain.SampleHeight(point);
            return point;
        }

        public bool can_place_grass(float x, float z)
        {
            if (routes.enabled && (routes.measure_road(x,z) < routes.layout.corridor_half_width ||
                routes.measure_river(x,z) < routes.layout.river_half_width + 2 ||
                routes.measure_courtyard(x,z) < routes.layout.landmark_clear_radius)) return false;
            return z - shoreline_z(x) > config.shore_blend_width &&
                (routes.enabled || Mathf.Abs(x - road_x(z)) > config.road_half_width + config.road_edge_width) &&
                talus_weight(x, z) < .35f &&
                terrain_data.GetSteepness(x / config.map_size, z / config.map_size) < config.slope_rock_start + 4;
        }

        public bool can_place_greenery(float x, float z)
        {
            if (!can_place_grass(x, z)) return false;
            return biome == null || !biome.upper_snow || terrain_data.GetInterpolatedHeight(x / config.map_size, z / config.map_size) < 6.5f;
        }

        public void Dispose()
        {
            if (terrain) { terrain.gameObject.SetActive(false); UnityEngine.Object.Destroy(terrain.gameObject); }
            if (terrain_data) UnityEngine.Object.Destroy(terrain_data);
            if (material_instance) UnityEngine.Object.Destroy(material_instance);
            foreach (var layer in layer_instances) if (layer) UnityEngine.Object.Destroy(layer);
        }
    }
}
