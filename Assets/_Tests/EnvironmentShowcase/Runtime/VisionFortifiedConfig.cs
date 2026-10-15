using System;
using UnityEngine;

namespace ZombieGame.Tests.EnvironmentShowcase
{
    /// <summary>Art-only city plan and isolated non-walkable Terrain cliffs. No battle or economy values.</summary>
    public sealed class VisionFortifiedConfig : ScriptableObject
    {
        [Serializable] public sealed class CliffRegion
        {
            public string name;
            public Vector2 center_m, radius_m;
            public float top_height_m, edge_width_m = 1.8f, talus_width_m = 2.5f;

            public float measure_edge(Vector2 point)
            {
                var delta = point-center_m;
                var normalized = new Vector2(delta.x/radius_m.x,delta.y/radius_m.y);
                float radius = delta.magnitude/Mathf.Max(.0001f,normalized.magnitude);
                float angle = Mathf.Atan2(normalized.y,normalized.x);
                float erosion = Mathf.Sin(angle*7)*.22f+Mathf.Sin(angle*13)*.10f;
                return (normalized.magnitude-1)*(normalized.magnitude < .0001f ? Mathf.Min(radius_m.x,radius_m.y) : radius)+erosion;
            }
        }

        [Serializable] public sealed class WallFootprint
        {
            public string id;
            public Vector2 center_m, half_size_m;
            public float yaw;
        }

        public string version = "vision-fortified-v4";
        public string source_record;
        public EnvironmentShowcaseConfig.SourceAsset wall_source;
        public Vector2 city_center_m = new Vector2(68,35);
        public Vector2 outer_center_offset_m, inner_center_offset_m;
        public Vector2 outer_size_m = new Vector2(44.1f,58);
        public Vector2 inner_size_m = new Vector2(35.96f,49.86f);
        public Vector2 portal_center_m = new Vector2(68,64);
        public float portal_clear_width_m = 6.489f;
        public bool portal_has_gate_leaf;
        public int expected_city_fixture_count = 7;
        public bool composition_only;
        public bool dense_repetition;
        public bool composition_overview_enabled;
        public Vector2 composition_overview_focus_m;
        public float composition_overview_focus_height_m, composition_overview_size_m;
        public float composition_overview_pitch_deg = 48, composition_overview_yaw_deg;
        public bool boundary_cliff_enabled;
        public float boundary_north_start_m = 122, boundary_side_start_m = 96;
        public float boundary_west_x_m = 38, boundary_east_x_m = 125.5f;
        public float boundary_rise_width_m = 2, boundary_top_m = 13.2f;
        public bool handoff_source_seams_pass;
        public float handoff_max_seam_gap_m = -1, handoff_max_footing_gap_m;
        public string handoff_seam_note = "Finite source ray probes only; a negative maximum gap means no exact analytical maximum-gap metric was authored.";
        public float patrol_clearance_m = 3;
        public string entrance_note = "Open north portal only. No approved gate leaf; no closing or friendly-only passage is implemented in this art preview.";
        public WallFootprint[] wall_footprints = Array.Empty<WallFootprint>();
        public string[] fixture_ids = Array.Empty<string>();
        public string[] fixture_source_ids = Array.Empty<string>();
        public bool[] human_city_fixtures = Array.Empty<bool>();
        public Vector2[] interior_clearance_points_m = Array.Empty<Vector2>();
        public Vector2[] protected_flat_points_m = Array.Empty<Vector2>();
        public Vector2[] detail_focus = Array.Empty<Vector2>();
        public CliffRegion[] cliffs = Array.Empty<CliffRegion>();
        public DarkWildernessConfig.SurfaceRegion[] courtyard_soil = Array.Empty<DarkWildernessConfig.SurfaceRegion>();
        public string missing_art = "No approved operable gate, intact civilian housing, independent barracks or warehouse source. Repeated existing house source is retained as-is, not a newly authored model. Long overhanging cliff modules and thick water-edge plants remain unavailable.";

        public void validate(bool require_wall)
        {
            if(outer_size_m.x <= inner_size_m.x || outer_size_m.y <= inner_size_m.y || inner_size_m.x <= 0 || inner_size_m.y <= 0 ||
                portal_clear_width_m < 3 || patrol_clearance_m < (dense_repetition ? 1.2f : 3) || portal_has_gate_leaf)
                throw new InvalidOperationException("Fortified preview bounds/clearance/open-portal policy invalid.");
            if(fixture_ids == null || fixture_source_ids == null || human_city_fixtures == null ||
                fixture_ids.Length != fixture_source_ids.Length || fixture_ids.Length != human_city_fixtures.Length || fixture_ids.Length < 16)
                throw new InvalidOperationException("Fortified preview requires an explicit source fixture manifest retaining the original sixteen roots.");
            if(require_wall && (wall_source == null || !wall_source.prefab || string.IsNullOrWhiteSpace(wall_source.source_record) || wall_footprints.Length == 0))
                throw new InvalidOperationException("Fortified city is waiting for a frozen Blender assembly handoff; no Unity replacement walls permitted.");
            if(composition_overview_enabled && (!dense_repetition || composition_overview_focus_m.x < 0 || composition_overview_focus_m.x > 128 ||
                composition_overview_focus_m.y < 0 || composition_overview_focus_m.y > 128 || composition_overview_focus_height_m < 0 ||
                composition_overview_focus_height_m > 12 || composition_overview_size_m < 20 || composition_overview_size_m > 60 ||
                composition_overview_pitch_deg < 15 || composition_overview_pitch_deg > 85 || Mathf.Abs(composition_overview_yaw_deg) > 180))
                throw new InvalidOperationException("Dense composition overview exceeds the isolated art-review camera scope.");
            if(boundary_cliff_enabled && (!dense_repetition || boundary_north_start_m < 112 || boundary_north_start_m > 124 ||
                boundary_side_start_m < 80 || boundary_side_start_m >= boundary_north_start_m || boundary_west_x_m < 30 ||
                boundary_east_x_m > 127 || boundary_east_x_m-boundary_west_x_m < 70 || boundary_rise_width_m < 1 ||
                boundary_rise_width_m > 3 || boundary_top_m <= 4.2f || boundary_top_m >= 17))
                throw new InvalidOperationException("Dense v6 connected boundary cliff exceeds the isolated 128m non-activity edge scope.");
            foreach(var cliff in cliffs)
                if(cliff == null || cliff.radius_m.x < 4 || cliff.radius_m.y < 4 || cliff.top_height_m <= 4.2f || cliff.top_height_m >= 17 || cliff.edge_width_m < 1 || cliff.talus_width_m < 1)
                    throw new InvalidOperationException("Fortified Terrain cliff region is invalid.");
        }

        public bool contains_city(float x,float z,float extra = 0)
        {
            var delta = new Vector2(x,z)-city_center_m-outer_center_offset_m;
            return Mathf.Abs(delta.x) < outer_size_m.x*.5f+extra && Mathf.Abs(delta.y) < outer_size_m.y*.5f+extra;
        }

        public float sample_height(float x,float z,float original)
        {
            var point = new Vector2(x,z);
            foreach(var cliff in cliffs)
            {
                float weight = 1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(-cliff.edge_width_m*.5f,cliff.edge_width_m*.5f,cliff.measure_edge(point)));
                original = Mathf.Lerp(original,cliff.top_height_m,weight);
            }
            if(boundary_cliff_enabled)
            {
                float weight = 1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(-boundary_rise_width_m,0,measure_boundary_edge(x,z)));
                original = Mathf.Lerp(original,boundary_top_m,weight);
            }
            return original;
        }

        /// <summary>One connected north cap with short east/west returns, not slopes across activity land.</summary>
        public float measure_boundary_edge(float x,float z)
        {
            return Mathf.Min(boundary_north_start_m-z,Mathf.Max(boundary_east_x_m-x,boundary_side_start_m-z),
                Mathf.Max(x-boundary_west_x_m,boundary_side_start_m-z));
        }

        public bool is_cliff_domain(float x,float z)
        {
            if(boundary_cliff_enabled && measure_boundary_edge(x,z) < .6f) return true;
            foreach(var cliff in cliffs)
                if(cliff.measure_edge(new Vector2(x,z)) < cliff.edge_width_m*.5f+cliff.talus_width_m) return true;
            return false;
        }

        public float measure_wall_edge(float x,float z)
        {
            float nearest = float.PositiveInfinity;
            foreach(var wall in wall_footprints)
            {
                var delta = new Vector2(x,z)-wall.center_m;
                float angle = wall.yaw*Mathf.Deg2Rad;
                var local = new Vector2(delta.x*Mathf.Cos(angle)-delta.y*Mathf.Sin(angle),delta.x*Mathf.Sin(angle)+delta.y*Mathf.Cos(angle));
                var outer = new Vector2(Mathf.Abs(local.x)-wall.half_size_m.x,Mathf.Abs(local.y)-wall.half_size_m.y);
                float distance = new Vector2(Mathf.Max(0,outer.x),Mathf.Max(0,outer.y)).magnitude+Mathf.Min(0,Mathf.Max(outer.x,outer.y));
                nearest = Mathf.Min(nearest,distance);
            }
            return nearest;
        }

        public float sample_wall_soil(float x,float z)
        {
            return (1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.2f,2.4f,Mathf.Abs(measure_wall_edge(x,z)))))*.72f;
        }

        public float sample_talus(float x,float z)
        {
            float weight = 0;
            if(boundary_cliff_enabled)
            {
                float edge = measure_boundary_edge(x,z);
                weight = Mathf.SmoothStep(0,1,Mathf.InverseLerp(-.3f,0,edge))*
                    (1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(0,3,edge)));
            }
            foreach(var cliff in cliffs)
            {
                float edge = cliff.measure_edge(new Vector2(x,z));
                float outside = Mathf.SmoothStep(0,1,Mathf.InverseLerp(-.2f,cliff.edge_width_m*.5f,edge));
                weight = Mathf.Max(weight,outside*(1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(cliff.edge_width_m*.5f,cliff.edge_width_m*.5f+cliff.talus_width_m,edge))));
            }
            return weight;
        }
    }
}
