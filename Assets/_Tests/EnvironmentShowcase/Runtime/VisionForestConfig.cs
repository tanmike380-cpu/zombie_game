using System;
using UnityEngine;

namespace ZombieGame.Tests.EnvironmentShowcase
{
    /// <summary>Art-only forest mass and natural clearings; never roads or navigation obstacles.</summary>
    public sealed class VisionForestConfig : ScriptableObject
    {
        [Serializable] public sealed class Clearing
        {
            public Vector2 center,radius;
        }

        [Serializable] public sealed class Corridor
        {
            public string name;
            public Vector2[] points;
            public float half_width = 4;
        }

        public string version = "vision-connected-forest-v1";
        public int tree_budget = 180;
        public float tree_spacing = 6.2f;
        public float chunk_size = 48;
        public float near_lod_screen_height = .22f;
        public float far_cull_screen_height = .025f;
        public float canopy_clearance = 5.3f;
        public float edge_feather = 2;
        public bool use_native_source_clearance;
        public Clearing[] forest_masses = Array.Empty<Clearing>();
        public Clearing[] clearings = Array.Empty<Clearing>();
        public Corridor[] corridors = Array.Empty<Corridor>();
        public Vector2[] review_focus = Array.Empty<Vector2>();

        public void validate()
        {
            if(tree_budget < 1 || tree_budget > 220 || tree_spacing < 5.5f || chunk_size < 24 ||
                canopy_clearance < 0 || edge_feather <= 0 || far_cull_screen_height <= 0 ||
                near_lod_screen_height <= far_cull_screen_height || near_lod_screen_height >= 1)
                throw new InvalidOperationException("Vision forest instance/crown/chunk budget is invalid.");
            validate_regions(forest_masses);
            validate_regions(clearings);
            if(corridors == null || corridors.Length < 2 || review_focus == null || review_focus.Length == 0)
                throw new InvalidOperationException("Vision forest requires connected natural clearings and review views.");
            foreach(var corridor in corridors)
            {
                if(corridor == null || corridor.points == null || corridor.points.Length < 2 || corridor.half_width < 3)
                    throw new InvalidOperationException("Vision natural clearing corridor is invalid.");
                foreach(var point in corridor.points) validate_point(point);
            }
            foreach(var point in review_focus) validate_point(point);
        }

        private static void validate_regions(Clearing[] regions)
        {
            if(regions == null) throw new InvalidOperationException("Vision forest region array missing.");
            foreach(var region in regions)
            {
                if(region == null || region.radius.x <= 0 || region.radius.y <= 0)
                    throw new InvalidOperationException("Vision forest region radius must be positive.");
                validate_point(region.center);
            }
        }

        private static void validate_point(Vector2 point)
        {
            if(point.x < 0 || point.y < 0 || point.x > 1 || point.y > 1)
                throw new InvalidOperationException("Vision forest normalized coordinate outside map: "+point);
        }

        public float measure_corridor_edge(Vector2 point,float size)
        {
            float edge = float.PositiveInfinity;
            foreach(var corridor in corridors)
            {
                for(int index=1;index<corridor.points.Length;index++)
                {
                    var start = corridor.points[index-1]*size;
                    var direction = corridor.points[index]*size-start;
                    float position = Mathf.Clamp01(Vector2.Dot(point-start,direction)/Mathf.Max(.0001f,direction.sqrMagnitude));
                    edge = Mathf.Min(edge,Vector2.Distance(point,start+direction*position)-corridor.half_width);
                }
            }
            return edge;
        }

        public float sample_weight(float x,float z,float size)
        {
            var point = new Vector2(x,z);
            float mass = 0;
            foreach(var region in forest_masses)
            {
                var delta = point-region.center*size;
                float distance = new Vector2(delta.x/region.radius.x,delta.y/region.radius.y).magnitude;
                float irregularity = (Mathf.PerlinNoise(x/13+8.4f,z/13)-.5f)*.12f;
                mass = Mathf.Max(mass,1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.82f,1.12f,distance+irregularity)));
            }
            float open_edge = measure_corridor_edge(point,size);
            foreach(var clearing in clearings)
            {
                var delta = point-clearing.center*size;
                var expanded = clearing.radius+Vector2.one*canopy_clearance;
                float distance = new Vector2(delta.x/expanded.x,delta.y/expanded.y).magnitude;
                float edge = (distance-1)*Mathf.Min(expanded.x,expanded.y);
                mass *= Mathf.SmoothStep(0,1,Mathf.InverseLerp(0,edge_feather,edge));
            }
            return mass*Mathf.SmoothStep(0,1,Mathf.InverseLerp(canopy_clearance,canopy_clearance+edge_feather,open_edge));
        }

        public bool has_source_clearance(ShowcaseConceptLayout concept,float x,float z)
        {
            if(!concept.can_place(x,z)) return false;
            if(use_native_source_clearance)
                return concept.footprints == null || concept.footprints.measure_native_source_edge(x,z) >= canopy_clearance;
            // Whole-crown presentation clearance, not a collider or a source-mesh deformation.
            for(int index=0;index<16;index++)
            {
                float angle = index*Mathf.PI/8;
                if(!concept.can_place(x+Mathf.Cos(angle)*canopy_clearance,z+Mathf.Sin(angle)*canopy_clearance)) return false;
            }
            return true;
        }
    }
}
