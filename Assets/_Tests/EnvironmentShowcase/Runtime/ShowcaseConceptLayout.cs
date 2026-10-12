using System;
using UnityEngine;

namespace ZombieGame.Tests.EnvironmentShowcase
{
    /// <summary>Concept-only terrain distance fields. No rivers, resource logic, navigation or world prop generation.</summary>
    public sealed class ShowcaseConceptLayout
    {
        public readonly ConceptLayoutConfig layout;
        private readonly float size;

        public ShowcaseConceptLayout(ConceptLayoutConfig source, float map_size)
        {
            layout = source; size = map_size;
        }

        public float coast_edge(float z) => layout.coast_x + (Mathf.PerlinNoise(z/19,7.1f)-.5f)*14 + Mathf.Sin(z/5)*1.4f;
        public float pond_edge_distance(float x, float z)
        {
            var delta = new Vector2(x-layout.pond_center.x*size,z-layout.pond_center.y*size);
            var normalized = new Vector2(delta.x/layout.pond_radius.x,delta.y/layout.pond_radius.y);
            float physical = delta.magnitude / Mathf.Max(.0001f,normalized.magnitude);
            float angle=Mathf.Atan2(normalized.y,normalized.x);
            float shore_offset=Mathf.Sin(angle*3+.3f)*.8f+Mathf.Sin(angle*7)*.32f;
            return (normalized.magnitude - 1) * (normalized.magnitude < .0001f ? layout.pond_radius.y : physical)-shore_offset;
        }

        public float sample_height(float x, float z)
        {
            float distance = x - coast_edge(z);
            float gentle = 3.8f + .32f*Mathf.Sin(x/18)*Mathf.Sin(z/21);
            var delta = new Vector2((x-layout.meadow_center.x*size)/layout.meadow_radius.x,(z-layout.meadow_center.y*size)/layout.meadow_radius.y);
            gentle += layout.meadow_height * Mathf.Exp(-delta.sqrMagnitude * 1.1f);
            // Coastal limestone shoulder varies along shore; actual heightfield, no overhang mesh.
            float coast_top = layout.coast_height - 2.3f*(.5f+.5f*Mathf.Sin(z/13))+1.1f*Mathf.Sin(z/5);
            float seabed = .08f + .84f*Mathf.SmoothStep(0,1,Mathf.InverseLerp(-10,0,distance));
            float rise_width=2.4f+2.4f*Mathf.PerlinNoise(z/12,3.7f);
            float coastal = Mathf.Lerp(seabed,coast_top,Mathf.SmoothStep(0,1,Mathf.InverseLerp(0,rise_width,distance)));
            float height = Mathf.Lerp(coastal,gentle,Mathf.SmoothStep(0,1,Mathf.InverseLerp(8,22,distance)));
            float edge = pond_edge_distance(x,z);
            if (edge < layout.pond_bank_width + 2)
            {
                float basin = layout.pond_level - layout.pond_depth + layout.pond_depth * Mathf.SmoothStep(0,1,Mathf.InverseLerp(-10,0,edge));
                basin += .55f * Mathf.SmoothStep(0,1,Mathf.InverseLerp(0,layout.pond_bank_width,edge));
                height = Mathf.Lerp(basin,height,Mathf.SmoothStep(0,1,Mathf.InverseLerp(layout.pond_bank_width,layout.pond_bank_width+2,edge)));
                if(edge>layout.pond_bank_width) height=Mathf.Max(height,layout.pond_level+.25f);
            }
            return height;
        }

        public float measure_wet_edge(float x, float z) => Mathf.Min(Mathf.Abs(x-coast_edge(z)),Mathf.Abs(pond_edge_distance(x,z)));
        public bool can_place(float x, float z) => x > coast_edge(z)+7 && pond_edge_distance(x,z) > 1;
        public float forest_weight(float x, float z)
        {
            if (!can_place(x,z)) return 0;
            float weight = .06f;
            foreach (var center in layout.forest_centers)
                weight = Mathf.Max(weight,1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(5,15,Vector2.Distance(new Vector2(x,z),center*size))));
            return weight;
        }

        public bool is_local_rock_patch(float x, float z)
        {
            // Small approved rocks only at two local meadow/shore patches, not a replacement cliff.
            return Vector2.Distance(new Vector2(x,z),new Vector2(.64f,.43f)*size)<7 ||
                Vector2.Distance(new Vector2(x,z),new Vector2(.70f,.64f)*size)<6 ||
                x-coast_edge(z)<11;
        }
    }
}
