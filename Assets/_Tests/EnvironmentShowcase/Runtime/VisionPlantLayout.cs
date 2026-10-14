using System;
using UnityEngine;

namespace ZombieGame.Tests.EnvironmentShowcase
{
    /// <summary>Source-specific natural beds and their explicit budgets; no mesh generation or extra plants.</summary>
    public static class VisionPlantLayout
    {
        public static DarkWildernessConfig.PlantPatch find_patch(DarkWildernessConfig preset,string source)
        {
            foreach(var patch in preset.vision_plant_patches)
                if(patch.source_id == source) return patch;
            throw new InvalidOperationException("Vision configured source distribution missing: "+source);
        }

        public static void validate_patches(DarkWildernessConfig preset)
        {
            if(preset.vision_plant_patches == null || preset.vision_plant_patches.Length == 0) return;
            int grass = 0;
            int shrubs = 0;
            foreach(var patch in preset.vision_plant_patches)
            {
                if(patch == null || string.IsNullOrEmpty(patch.source_id) || patch.count < 0 || patch.centers == null || patch.radii == null ||
                    patch.centers.Length == 0 || patch.centers.Length != patch.radii.Length || patch.scale_range.x <= 0 || patch.scale_range.y < patch.scale_range.x)
                    throw new InvalidOperationException("Vision source bed configuration invalid.");
                for(int index=0;index<patch.radii.Length;index++)
                    if(patch.radii[index].x <= 0 || patch.radii[index].y <= 0 || patch.centers[index].x < 0 || patch.centers[index].x > 1 || patch.centers[index].y < 0 || patch.centers[index].y > 1)
                        throw new InvalidOperationException("Vision source bed radius/center invalid: "+patch.source_id);
                if(patch.category == "grass") grass += patch.count;
                else if(patch.category == "shrub") shrubs += patch.count;
                else throw new InvalidOperationException("Unsupported Vision bed category: "+patch.category);
            }
            if(grass != preset.vision_grass_budget || shrubs != preset.vision_shrub_budget)
                throw new InvalidOperationException("Vision source counts must preserve total grass/shrub budgets.");
        }

        public static Vector2 sample_candidate(DarkWildernessConfig.PlantPatch patch,System.Random random,float size)
        {
            int index = random.Next(patch.centers.Length);
            var center = patch.centers[index]*size;
            var radius = patch.radii[index];
            return center+new Vector2(((float)random.NextDouble()*2-1)*radius.x,((float)random.NextDouble()*2-1)*radius.y);
        }

        public static float measure_patch(DarkWildernessConfig.PlantPatch patch,float x,float z,float size)
        {
            float weight = 0;
            for(int index=0;index<patch.centers.Length;index++)
            {
                var delta = new Vector2(x,z)-patch.centers[index]*size;
                var radius = patch.radii[index];
                float distance = new Vector2(delta.x/radius.x,delta.y/radius.y).magnitude;
                float edge = (Mathf.PerlinNoise(x/2.7f+4,z/2.7f)-.5f)*.2f;
                weight = Mathf.Max(weight,1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.5f,1.05f,distance+edge)));
            }
            return weight;
        }

        public static float measure_core_beds(DarkWildernessConfig preset,float x,float z,float size)
        {
            float weight = 0;
            if(preset.vision_plant_patches == null) return weight;
            foreach(var patch in preset.vision_plant_patches)
                if(patch.core_bed) weight = Mathf.Max(weight,measure_patch(patch,x,z,size));
            return weight;
        }

        public static float nominal_area(DarkWildernessConfig.PlantPatch patch)
        {
            float area = 0;
            foreach(var radius in patch.radii) area += Mathf.PI*radius.x*radius.y;
            return area;
        }
    }
}
