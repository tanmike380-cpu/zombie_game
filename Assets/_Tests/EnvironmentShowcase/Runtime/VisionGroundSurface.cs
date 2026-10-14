using UnityEngine;

namespace ZombieGame.Tests.EnvironmentShowcase
{
    /// <summary>Authored broad natural surface regions, independent of roads, units and terrain relief.</summary>
    public static class VisionGroundSurface
    {
        public static void sample_weights(EnvironmentShowcaseConfig config,ShowcaseConceptLayout concept,TerrainData terrain,float u,float v,float[] weights)
        {
            var preset = config.active_dark_layout;
            float x = u*config.map_size;
            float z = v*config.map_size;
            float core_bed = VisionPlantLayout.measure_core_beds(preset,x,z,config.map_size);
            float forest = concept.forest_weight(x,z);
            float green_region = Mathf.Max(measure_regions(preset.vision_meadow_regions,x,z,config.map_size),core_bed);
            float soil = Mathf.Max(preset.vision_base_soil,measure_regions(preset.vision_soil_regions,x,z,config.map_size));
            soil *= 1-green_region*.70f;
            // Keep legacy placement exclusions untouched; only their visual soil wash is reduced.
            soil = Mathf.Max(soil,concept.exposed_soil(x,z)*.18f*(1-green_region*.7f));
            soil = Mathf.Max(soil,(concept.footprints?.soil_weight(x,z) ?? 0)*.60f);
            // Leaf litter is local under the original oak groups, not a map-wide bare soil wash.
            soil = Mathf.Max(soil,forest*.26f);
            float vegetation = concept.vegetation_weight(x,z);
            vegetation = Mathf.Max(vegetation,core_bed);
            float dry = measure_regions(preset.vision_dry_regions,x,z,config.map_size)*(1-vegetation*.55f)*(1-green_region*.7f);
            float wet = (1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.15f,2.3f,concept.measure_wet_edge(x,z))))*.75f;
            float stone = Mathf.SmoothStep(0,1,Mathf.InverseLerp(18,42,terrain.GetSteepness(u,v)))*.9f;
            float coast_distance = x-concept.coast_edge(z);
            if(coast_distance<0)
            {
                // Seabed has real mud/mineral layers, not the invisible grass assigned by prior inland presets.
                soil = .55f;
                dry = 0;
                wet = .55f;
                stone = .28f;
            }
            weights[0] = (1-soil)*(1-dry)*(1-wet)*(1-stone);
            weights[1] = (1-soil)*dry*(1-wet)*(1-stone);
            weights[2] = soil*(1-wet)*(1-stone);
            weights[3] = wet*(1-stone);
            weights[4] = stone;
            weights[5] = 0;
        }

        private static float measure_regions(DarkWildernessConfig.SurfaceRegion[] regions,float x,float z,float size)
        {
            float weight = 0;
            foreach(var region in regions)
            {
                var delta = new Vector2(x,z)-region.center*size;
                float distance = new Vector2(delta.x/region.radius.x,delta.y/region.radius.y).magnitude;
                float broken_edge = (Mathf.PerlinNoise(x/8+5.1f,z/8)-.5f)*.25f;
                float mask = 1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.5f,1.25f,distance+broken_edge));
                weight = Mathf.Max(weight,mask*region.strength);
            }
            return weight;
        }
    }
}
