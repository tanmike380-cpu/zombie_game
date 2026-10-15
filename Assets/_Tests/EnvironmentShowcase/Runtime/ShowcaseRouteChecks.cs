using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace ZombieGame.Tests.EnvironmentShowcase
{
    public sealed partial class ShowcaseChecks
    {
        private IEnumerator run_route_checks()
        {
            showcase.foliage.show_grass = true;
            report.routes = new RouteReport { enabled = true, version = showcase.config.route_layout.version,
                previous_baseline = "Builds/ArtReview/EnvironmentShowcase/cliffs-final; -showcaseLayout cliffs restores geometry/forest baseline",
                license_note = "Mixed: farmhouse CC BY 4.0; wall atlas CC BY-SA 3.0; 3TD/clay CC0. User ores license unverified; private preview only, no public upload." };
            inspect_river_routes();
            inspect_road_routes();
            inspect_forest_routes();
            var fixtures = new List<FixtureReport>();
            for (int index = 0; index < 9; index++)
            {
                bool building = index < 3;
                int fixture_index = building ? index : index - 3;
                var instance = building ? showcase.landmarks.buildings[fixture_index] : showcase.landmarks.ores[fixture_index];
                var point = building ? showcase.config.route_layout.landmarks[fixture_index] : showcase.config.route_layout.ores[fixture_index];
                showcase.set_focus(point,0);
                for (int frame = 0; frame < 12; frame++) yield return null;
                yield return new WaitForEndOfFrame();
                fixtures.Add(inspect_fixture(instance,index,building));
                for (int zoom = 1; zoom < 3; zoom++)
                {
                    showcase.set_focus(point,zoom);
                    yield return new WaitForEndOfFrame();
                    save_camera_image("fixture-" + index + "-" + (zoom == 1 ? "middle" : "far") + ".png");
                }
            }
            report.routes.fixtures = fixtures.ToArray();
            report.routes.fixtures_pass = fixtures.Count == 9;
            foreach (var fixture in fixtures) report.routes.fixtures_pass &= fixture.source_transform_pass && fixture.ground_pass && fixture.clearance_pass && fixture.pixel_pass && fixture.material_channels_pass;
            showcase.set_view(8,0);
            yield return new WaitForEndOfFrame();
            report.routes.water_mean_rgb_delta = compare_same_frame_renderer(showcase.water_renderer,"river-water");
            report.routes.water_pixel_pass = report.routes.water_mean_rgb_delta > .0001f;
            showcase.set_view(9,0);
            yield return new WaitForEndOfFrame();
            report.routes.forest_mean_rgb_delta = compare_same_frame_forest();
            report.routes.forest_pixel_pass = report.trees_intentionally_absent ? report.routes.forest_mean_rgb_delta < .00001f : report.routes.forest_mean_rgb_delta > .001f;
            for (int zoom = 0; zoom < 3; zoom++)
            {
                showcase.set_view(12,zoom);
                for (int frame = 0; frame < 60; frame++) yield return null;
                var samples = new float[180];
                for (int frame = 0; frame < samples.Length; frame++) { yield return null; samples[frame] = Time.unscaledDeltaTime * 1000; }
                timings.Add(build_timing(samples,zoom,true,"routes-" + new[] { "near","middle","far" }[zoom]));
                yield return new WaitForEndOfFrame();
                save_camera_image("routes-" + new[] { "near","middle","far" }[zoom] + ".png");
            }
            report.timings = timings.ToArray();
            var routes = report.routes;
            routes.passed = routes.river_bed_pass && routes.ford_pass && routes.road_slope_pass && routes.crossing_pass &&
                routes.forest_coverage_pass && routes.forest_pixel_pass && routes.corridor_width_pass && routes.water_pixel_pass && routes.fixtures_pass;
        }

        private void inspect_river_routes()
        {
            var layout = showcase.config.route_layout;
            var ground = showcase.ground;
            var routes = report.routes;
            routes.minimum_river_depth_m = float.PositiveInfinity;
            var river_points = ground.routes.river_points;
            for (int segment = 1; segment < river_points.Length; segment++)
                for (int index = 0; index <= 20; index++)
                {
                    var point = Vector2.Lerp(river_points[segment - 1],river_points[segment],index / 20f);
                    float height = ground.terrain_data.GetInterpolatedHeight(point.x,point.y);
                    routes.minimum_river_depth_m = Mathf.Min(routes.minimum_river_depth_m,showcase.config.water_level - height);
                }
            routes.river_bed_pass = routes.minimum_river_depth_m > .05f;
            routes.ford_depth_m = showcase.config.water_level - ground.terrain_data.GetInterpolatedHeight(layout.ford.x,layout.ford.y);
            routes.ford_pass = Mathf.Abs(routes.ford_depth_m - layout.ford_depth) < .04f;
        }

        private void inspect_road_routes()
        {
            var layout = showcase.config.route_layout;
            var ground = showcase.ground;
            var routes = report.routes;
            routes.crossing_pass = routes.corridor_width_pass = true;
            routes.minimum_corridor_width_m = layout.corridor_half_width * 2;
            foreach (var road in ground.routes.road_paths)
                for (int segment = 1; segment < road.Length; segment++)
                    for (int index = 0; index <= 30; index++)
                    {
                        var point = Vector2.Lerp(road[segment - 1],road[segment],index / 30f);
                        routes.maximum_road_slope_degrees = Mathf.Max(routes.maximum_road_slope_degrees,ground.terrain_data.GetSteepness(point.x,point.y));
                        float river_distance = ground.routes.measure_river(point.x * showcase.config.map_size,point.y * showcase.config.map_size);
                        if (river_distance < layout.river_half_width && Vector2.Distance(point,layout.ford) * showcase.config.map_size > layout.ford_radius + 1)
                            routes.crossing_pass = false;
                        var tangent = (road[segment] - road[segment - 1]).normalized;
                        var offset = new Vector2(-tangent.y,tangent.x) * (layout.corridor_half_width - .5f) / showcase.config.map_size;
                        foreach (var side in new[] { point - offset,point + offset })
                            if (ground.forest_weight(side.x * showcase.config.map_size,side.y * showcase.config.map_size) > .01f ||
                                ground.terrain_data.GetSteepness(side.x,side.y) > 35) routes.corridor_width_pass = false;
                    }
            // Geometric art corridor only: not an authored UnitBalance/NavMesh slope limit.
            routes.road_slope_pass = routes.maximum_road_slope_degrees < 32;
        }

        private void inspect_forest_routes()
        {
            if (report.trees_intentionally_absent) { report.routes.forest_coverage_pass = true; return; }
            int sampled = 0, covered = 0;
            foreach (var band in showcase.config.route_layout.forest_bands)
                for (int segment = 1; segment < band.points.Length; segment++)
                    for (int index = 0; index < 30; index++)
                    {
                        var center = Vector2.Lerp(band.points[segment - 1],band.points[segment],index / 29f) * showcase.config.map_size;
                        var tangent = (band.points[segment] - band.points[segment - 1]).normalized;
                        var normal = new Vector2(-tangent.y,tangent.x);
                        for (int across = -1; across <= 1; across++)
                        {
                            var point = center + normal * across * showcase.config.route_layout.forest_half_width * .45f;
                            if (showcase.ground.forest_weight(point.x,point.y) < .75f || !showcase.ground.can_place_greenery(point.x,point.y)) continue;
                            sampled++;
                            foreach (var tree in showcase.foliage.tree_positions)
                                if (Vector2.Distance(point,tree) < 3.2f) { covered++; break; }
                        }
                    }
            report.routes.forest_coverage = sampled == 0 ? 0 : covered / (float)sampled;
            report.routes.forest_coverage_pass = sampled > 40 && report.routes.forest_coverage >= .8f;
        }

        private FixtureReport inspect_fixture(GameObject instance, int index, bool building)
        {
            var position = instance.transform.position;
            float x = position.x - showcase.ground.origin.x, z = position.z - showcase.ground.origin.z;
            var fixture = new FixtureReport { id = instance.name, source = showcase.landmarks.sources[index],
                authored_native_dimensions_m = showcase.landmarks.expected_dimensions[index], world_position = position,
                whole_uniform_scale = building ? 1 : showcase.config.route_layout.ore_uniform_scale,
                ground_height_error_m = Mathf.Abs(position.y - showcase.ground.sample_ground(x,z).y) };
            var sources = building ? showcase.config.landmark_sources : showcase.config.ore_sources;
            var source = Array.Find(sources,candidate => instance.name.StartsWith(candidate.id + " ",StringComparison.Ordinal));
            if (source == null) throw new InvalidOperationException("Runtime neutral fixture not matched to source: " + instance.name);
            fixture.source_transform_pass = source != null && compare_source_transforms(instance,source.prefab) &&
                Vector3.Distance(instance.transform.localScale,source.prefab.transform.localScale * fixture.whole_uniform_scale) < .00001f;
            fixture.whole_root_translation = position - source.prefab.transform.position;
            fixture.material_channels_pass = inspect_material_bindings(source,building,fixture);
            fixture.footprint_ground_span_m = measure_footprint_ground(instance,source,fixture.whole_uniform_scale);
            fixture.ground_pass = fixture.ground_height_error_m < .003f && position.y > showcase.config.water_level &&
                instance.GetComponentsInChildren<Collider>(true).Length == 0 && fixture.footprint_ground_span_m < (building ? .08f : .5f);
            fixture.clearance_pass = showcase.ground.routes.measure_river(x,z) > showcase.config.route_layout.river_half_width + 2 &&
                (building || (showcase.ground.routes.measure_road(x,z) > showcase.config.route_layout.corridor_half_width &&
                    showcase.ground.routes.measure_courtyard(x,z) > showcase.config.route_layout.landmark_clear_radius));
            fixture.pixel_delta = compare_same_frame_object(instance,"fixture-" + index);
            fixture.pixel_pass = fixture.pixel_delta > (building ? .0001f : .00001f);
            return fixture;
        }

        /// <summary>Compare actual player material bindings with checksummed source references and editor import evidence.</summary>
        private static bool inspect_material_bindings(EnvironmentShowcaseConfig.SourceAsset source, bool building, FixtureReport fixture)
        {
            if (source.material_bindings == null || source.material_bindings.Length == 0) return false;
            bool passed = true;
            foreach (var binding in source.material_bindings)
            {
                var material = binding.material;
                if (!material) return false;
                passed &= binding.import_channels_pass && material.shader.name == "ZombieGame/EnvironmentShowcase/SourceOpaque" &&
                    material.GetTexture("_MainTex") == binding.base_color && binding.base_color != null;
                if (binding.normal) passed &= material.GetTexture("_BumpMap") == binding.normal && material.GetFloat("_BumpScale") > 0;
                if (binding.roughness) passed &= material.GetTexture("_Roughness") == binding.roughness && material.GetFloat("_HasRoughness") == 1;
                if (building) passed &= !binding.metallic && material.GetFloat("_HasMetallic") == 0 && material.GetFloat("_MetallicConstant") == 0;
                else
                {
                    passed &= binding.normal != null && binding.roughness != null && binding.metallic != null && binding.metallic_max_red > 0 &&
                        material.GetTexture("_MetallicMap") == binding.metallic && material.GetFloat("_HasMetallic") == 1 &&
                        material.GetFloat("_MetallicMultiplier") == binding.metallic_multiplier && binding.metallic_multiplier == 1;
                    fixture.metallic_max_red = Mathf.Max(fixture.metallic_max_red,binding.metallic_max_red);
                    fixture.normal_note = binding.normal_note;
                }
            }
            return passed;
        }

        private float measure_footprint_ground(GameObject instance, EnvironmentShowcaseConfig.SourceAsset source, float scale)
        {
            float error = 0;
            var yaw = instance.transform.rotation * Quaternion.Inverse(source.prefab.transform.localRotation);
            for (int z = -1; z <= 1; z++)
                for (int x = -1; x <= 1; x++)
                {
                    var offset = source.native_bounds_center + new Vector3(x * source.native_dimensions.x * .5f,
                        -source.native_dimensions.y * .5f,z * source.native_dimensions.z * .5f);
                    if(source.has_authored_ground_anchor) offset.y = source.authored_ground_anchor_y-source.authored_ground_offset_y;
                    var point = instance.transform.position + yaw * offset * scale;
                    float height = showcase.ground.sample_ground(point.x - showcase.ground.origin.x,point.z - showcase.ground.origin.z).y;
                    error = Mathf.Max(error,Mathf.Abs(point.y - height));
                }
            return error;
        }

        private static bool compare_source_transforms(GameObject instance, GameObject source)
        {
            var actual = instance.GetComponentsInChildren<MeshFilter>(true);
            var expected = source.GetComponentsInChildren<MeshFilter>(true);
            if (actual.Length != expected.Length) return false;
            for (int index = 0; index < actual.Length; index++)
            {
                if (actual[index].sharedMesh != expected[index].sharedMesh) return false;
                var actual_materials = actual[index].GetComponent<MeshRenderer>().sharedMaterials;
                var expected_materials = expected[index].GetComponent<MeshRenderer>().sharedMaterials;
                if (actual_materials.Length != expected_materials.Length) return false;
                for (int slot = 0; slot < actual_materials.Length; slot++)
                    if (actual_materials[slot] != expected_materials[slot]) return false;
                var actual_matrix = instance.transform.worldToLocalMatrix * actual[index].transform.localToWorldMatrix;
                var expected_matrix = source.transform.worldToLocalMatrix * expected[index].transform.localToWorldMatrix;
                for (int component = 0; component < 16; component++)
                    if (Mathf.Abs(actual_matrix[component] - expected_matrix[component]) > .0001f) return false;
            }
            return true;
        }

        private float compare_same_frame_object(GameObject instance, string prefix)
        {
            bool previous = instance.activeSelf;
            var visible = capture_camera();
            Texture2D hidden = null;
            try
            {
                instance.SetActive(false);
                hidden = capture_camera();
                File.WriteAllBytes(Path.Combine(output_directory,prefix + "-on.png"),visible.EncodeToPNG());
                File.WriteAllBytes(Path.Combine(output_directory,prefix + "-off.png"),hidden.EncodeToPNG());
                return measure_pixel_delta(visible.GetPixels32(),hidden.GetPixels32());
            }
            finally { instance.SetActive(previous); Destroy(visible); if (hidden) Destroy(hidden); }
        }

        private float compare_same_frame_renderer(Renderer renderer, string prefix)
        {
            bool previous = renderer.enabled;
            var visible = capture_camera();
            Texture2D hidden = null;
            try
            {
                renderer.enabled = false;
                hidden = capture_camera();
                File.WriteAllBytes(Path.Combine(output_directory,prefix + "-on.png"),visible.EncodeToPNG());
                File.WriteAllBytes(Path.Combine(output_directory,prefix + "-off.png"),hidden.EncodeToPNG());
                return measure_pixel_delta(visible.GetPixels32(),hidden.GetPixels32());
            }
            finally { renderer.enabled = previous; Destroy(visible); if (hidden) Destroy(hidden); }
        }

        private float compare_same_frame_forest()
        {
            bool previous = showcase.foliage.show_trees;
            showcase.foliage.show_trees = true;
            var visible = capture_camera();
            Texture2D hidden = null;
            try
            {
                showcase.foliage.show_trees = false;
                hidden = capture_camera();
                File.WriteAllBytes(Path.Combine(output_directory,"forest-belt-on.png"),visible.EncodeToPNG());
                File.WriteAllBytes(Path.Combine(output_directory,"forest-belt-off.png"),hidden.EncodeToPNG());
                return measure_pixel_delta(visible.GetPixels32(),hidden.GetPixels32());
            }
            finally { showcase.foliage.show_trees = previous; Destroy(visible); if (hidden) Destroy(hidden); }
        }
    }
}
