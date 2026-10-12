using System;
using UnityEngine;

namespace ZombieGame.Tests.EnvironmentShowcase
{
    /// <summary>Shared authored route distances and height fixtures, not routing or collision gameplay.</summary>
    public sealed class ShowcaseRoutes
    {
        private readonly EnvironmentShowcaseConfig config;
        public EnvironmentShowcaseConfig.RouteLayout layout => config.route_layout;
        public bool enabled => layout != null && layout.enabled;
        public readonly Vector2[] river_points;
        public readonly Vector2[][] road_paths;
        public ShowcaseRoutes(EnvironmentShowcaseConfig settings)
        {
            config = settings;
            river_points = build_curve(layout.river);
            road_paths = new Vector2[layout.roads.Length][];
            for (int index = 0; index < road_paths.Length; index++) road_paths[index] = build_curve(layout.roads[index].points);
        }

        private static Vector2[] build_curve(Vector2[] points)
        {
            const int SUBDIVISIONS = 8;
            var curve = new Vector2[(points.Length - 1) * SUBDIVISIONS + 1];
            for (int segment = 0; segment < points.Length - 1; segment++)
                for (int sample = 0; sample < SUBDIVISIONS; sample++)
                {
                    float fraction = sample / (float)SUBDIVISIONS;
                    var first = points[Mathf.Max(0,segment - 1)];
                    var second = points[segment];
                    var third = points[segment + 1];
                    var fourth = points[Mathf.Min(points.Length - 1,segment + 2)];
                    curve[segment * SUBDIVISIONS + sample] = .5f * ((2 * second) + (-first + third) * fraction +
                        (2 * first - 5 * second + 4 * third - fourth) * fraction * fraction +
                        (-first + 3 * second - 3 * third + fourth) * fraction * fraction * fraction);
                }
            curve[curve.Length - 1] = points[points.Length - 1];
            foreach (var point in curve)
                if (point.x < 0 || point.y < 0 || point.x > 1 || point.y > 1)
                    throw new InvalidOperationException("Authored route curve overshoots terrain bounds; adjust nodes, do not clamp: " + point);
            return curve;
        }

        public static float measure_path(Vector2 point, Vector2[] path, float size)
        {
            float distance = float.PositiveInfinity;
            for (int index = 1; index < path.Length; index++)
            {
                var first = path[index - 1] * size;
                var segment = (path[index] - path[index - 1]) * size;
                float fraction = Mathf.Clamp01(Vector2.Dot(point - first, segment) / Mathf.Max(.0001f, segment.sqrMagnitude));
                distance = Mathf.Min(distance, Vector2.Distance(point, first + segment * fraction));
            }
            return distance;
        }

        public float measure_river(float x, float z) => measure_path(new Vector2(x,z), river_points, config.map_size);
        public float measure_road(float x, float z)
        {
            float distance = float.PositiveInfinity;
            foreach (var road in road_paths) distance = Mathf.Min(distance, measure_path(new Vector2(x,z), road, config.map_size));
            return distance;
        }

        public float measure_courtyard(float x, float z)
        {
            float distance = float.PositiveInfinity;
            foreach (var center in layout.landmarks) distance = Mathf.Min(distance, Vector2.Distance(new Vector2(x,z), center * config.map_size));
            return distance;
        }

        public float measure_forest(float x, float z)
        {
            float distance = float.PositiveInfinity;
            foreach (var band in layout.forest_bands) distance = Mathf.Min(distance, measure_path(new Vector2(x,z), band.points, config.map_size));
            return distance;
        }

        public float sample_forest(float x, float z)
        {
            if (measure_road(x,z) < layout.corridor_half_width || measure_courtyard(x,z) < layout.landmark_clear_radius ||
                measure_river(x,z) < layout.river_half_width + 2) return 0;
            return 1 - Mathf.SmoothStep(0,1,Mathf.InverseLerp(layout.forest_half_width - 2, layout.forest_half_width + 1, measure_forest(x,z)));
        }

        public float sample_height(float x, float z, float original, float highland_base)
        {
            var point = new Vector2(x,z);
            var delta = Vector2.Scale(point - layout.highland_center * config.map_size,
                new Vector2(1 / layout.highland_radius.x, 1 / layout.highland_radius.y));
            float radius = delta.magnitude;
            float physical_radius = Mathf.Max(Mathf.Min(layout.highland_radius.x,layout.highland_radius.y),
                Vector2.Scale(delta,layout.highland_radius).magnitude / Mathf.Max(radius,.0001f));
            float highland_weight = 1 - Mathf.SmoothStep(0,1,Mathf.InverseLerp(-layout.highland_edge_width,0,(radius - 1) * physical_radius));
            float height = Mathf.Lerp(original, Mathf.Max(highland_base, layout.highland_height), highland_weight);
            float river_distance = measure_river(x,z);
            float river_weight = 1 - Mathf.SmoothStep(0,1,Mathf.InverseLerp(layout.river_half_width, layout.river_half_width + layout.river_bank_width, river_distance));
            float ford_weight = 1 - Mathf.SmoothStep(0,1,Mathf.InverseLerp(layout.ford_radius, layout.ford_radius + 4,
                Vector2.Distance(point,layout.ford * config.map_size)));
            float bed_height = config.water_level - Mathf.Lerp(layout.river_depth, layout.ford_depth, ford_weight);
            height = Mathf.Lerp(height, bed_height, river_weight);
            float road_weight = 1 - Mathf.SmoothStep(0,1,Mathf.InverseLerp(layout.corridor_half_width, layout.corridor_half_width + 2, measure_road(x,z)));
            float road_height = Mathf.Lerp(layout.road_height, bed_height, river_weight);
            height = Mathf.Lerp(height, road_height, road_weight);
            float courtyard = 1 - Mathf.SmoothStep(0,1,Mathf.InverseLerp(layout.landmark_pad_radius,layout.landmark_clear_radius,measure_courtyard(x,z)));
            return Mathf.Lerp(height, layout.road_height, courtyard);
        }
    }
}
