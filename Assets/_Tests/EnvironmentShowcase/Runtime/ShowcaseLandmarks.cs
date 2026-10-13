using System;
using System.Collections.Generic;
using UnityEngine;

namespace ZombieGame.Tests.EnvironmentShowcase
{
    /// <summary>Nine neutral source-assembly fixtures, no mining, scripts, blocking or assembly refits.</summary>
    public sealed class ShowcaseLandmarks : IDisposable
    {
        public readonly List<GameObject> buildings = new List<GameObject>();
        public readonly List<GameObject> ores = new List<GameObject>();
        public readonly List<string> sources = new List<string>();
        public readonly List<Vector3> expected_dimensions = new List<Vector3>();
        public readonly List<EnvironmentShowcaseConfig.SourceAsset> placed_sources=new List<EnvironmentShowcaseConfig.SourceAsset>();
        public readonly List<DarkWildernessConfig.Placement> placements=new List<DarkWildernessConfig.Placement>();
        public bool show_buildings = true, show_ores = true;

        public ShowcaseLandmarks(EnvironmentShowcaseConfig config, ShowcaseTerrain ground, Transform parent)
        {
            if(config.dark_enabled)
            {
                if(!config.dark_layout.props_enabled) return;
                foreach(var placement in config.dark_layout.placements)
                {
                    var source=config.dark_layout.find_source(config,placement.source_id);
                    var instance=place_source(source,placement.center,placement.yaw,placement.uniform_scale,config,ground,parent);
                    if(placement.ore) ores.Add(instance); else buildings.Add(instance);
                    placed_sources.Add(source);placements.Add(placement);
                }
                return;
            }
            if (!ground.routes.enabled) return;
            var layout = config.route_layout;
            for (int index = 0; index < layout.landmarks.Length; index++)
            {
                int source_index = ground.biome == null ? 0 : ground.biome.landmark_source_indices[index];
                buildings.Add(place_source(config.landmark_sources[source_index], layout.landmarks[index], index * 23, 1, config, ground, parent));
            }
            for (int index = 0; index < layout.ores.Length; index++)
                ores.Add(place_source(config.ore_sources[index < 3 ? 0 : 1], layout.ores[index], index * 47, layout.ore_uniform_scale, config, ground, parent));
        }

        private GameObject place_source(EnvironmentShowcaseConfig.SourceAsset source, Vector2 center, float yaw, float uniform_scale,
            EnvironmentShowcaseConfig config, ShowcaseTerrain ground, Transform parent)
        {
            if (!source.prefab || string.IsNullOrWhiteSpace(source.source_record))
                throw new InvalidOperationException("Neutral fixture missing approved source: " + source.id);
            var instance = UnityEngine.Object.Instantiate(source.prefab, parent);
            instance.name = source.id + " — neutral art fixture, whole scale " + uniform_scale;
            var rotation = Quaternion.Euler(0,yaw,0);
            var source_offset = rotation * new Vector3(source.native_bounds_center.x,source.native_min_y,source.native_bounds_center.z) * uniform_scale;
            // One whole-root translation centers its authored footprint on the pad; children are never refitted.
            instance.transform.position = ground.sample_ground(center.x * config.map_size, center.y * config.map_size) - source_offset;
            instance.transform.rotation = rotation * source.prefab.transform.localRotation;
            instance.transform.localScale = source.prefab.transform.localScale * uniform_scale;
            if (instance.GetComponentsInChildren<Collider>(true).Length != 0)
                throw new InvalidOperationException("Neutral art fixture unexpectedly adds blocking: " + source.id);
            sources.Add(source.source_record);
            expected_dimensions.Add(source.native_dimensions);
            return instance;
        }

        public void update_visibility()
        {
            foreach (var building in buildings) building.SetActive(show_buildings);
            foreach (var ore in ores) ore.SetActive(show_ores);
        }

        public void Dispose()
        {
            foreach (var building in buildings) { building.SetActive(false); UnityEngine.Object.Destroy(building); }
            foreach (var ore in ores) { ore.SetActive(false); UnityEngine.Object.Destroy(ore); }
        }
    }
}
