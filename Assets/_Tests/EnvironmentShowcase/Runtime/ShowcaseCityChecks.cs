using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace ZombieGame.Tests.EnvironmentShowcase
{
    public sealed partial class ShowcaseChecks
    {
        [Serializable] private sealed class NeutralComparisonReport
        {
            public string scope = "Same v5 geometry/camera, original v4 light versus neutral v5 light. This is not a reference-image reconstruction or an art approval.";
            public string plane_scope = "All dry inland heightfield samples beyond the actual sea/closed-pond bank plus a 0.55m Terrain interpolation margin; no cliff/plateau region exclusions. Sea/pond bottoms and bank transitions are counted separately, not called flat activity land.";
            public int inland_samples,water_or_bank_samples,cliff_masks;
            public float maximum_inland_height_error_m,maximum_inland_slope_degrees;
            public bool full_inland_plane_pass,lighting_response_pass,lighting_restored,technical_pass,art_visual_accepted;
            public string before_screenshot,after_screenshot;
            public float pixel_delta,before_mean_luma,after_mean_luma,before_black_fraction,after_black_fraction,before_white_fraction,after_white_fraction;
            public Vector3 camera_position,camera_euler;
            public float orthographic_size;
            public LightState before_light,after_light;
        }

        [Serializable] private sealed class LightState
        {
            public string ambient_mode,shadow_mode;
            public Color sun,ambient,sky,equator,ground,fog;
            public float sun_intensity,shadow_strength,ambient_intensity;
            public Vector3 sun_euler;
        }

        private IEnumerator run_neutral_comparison_checks()
        {
            var preset = showcase.config.active_dark_layout;
            if(!preset.neutral_light_baseline) yield break;
            var evidence = measure_complete_inland_plane();
            showcase.set_view(0,1);
            for(int frame=0;frame<30;frame++) yield return null;
            yield return new WaitForEndOfFrame();
            var camera = showcase.review_camera;
            evidence.camera_position = camera.transform.position;
            evidence.camera_euler = camera.transform.eulerAngles;
            evidence.orthographic_size = camera.orthographicSize;
            var actual = capture_light_state();
            Texture2D before = null,after = null;
            try
            {
                apply_source_light(preset.neutral_light_baseline);
                evidence.before_light = capture_light_state();
                before = capture_camera();
                restore_light_state(actual);
                evidence.after_light = capture_light_state();
                after = capture_camera();
                evidence.before_screenshot = "neutral-daylight-same-camera-v4-light.png";
                evidence.after_screenshot = "neutral-daylight-same-camera-v5-light.png";
                File.WriteAllBytes(Path.Combine(output_directory,evidence.before_screenshot),before.EncodeToPNG());
                File.WriteAllBytes(Path.Combine(output_directory,evidence.after_screenshot),after.EncodeToPNG());
                var before_pixels = before.GetPixels32();
                var after_pixels = after.GetPixels32();
                measure_light_pixels(before_pixels,out evidence.before_mean_luma,out evidence.before_black_fraction,out evidence.before_white_fraction);
                measure_light_pixels(after_pixels,out evidence.after_mean_luma,out evidence.after_black_fraction,out evidence.after_white_fraction);
                evidence.pixel_delta = measure_pixel_delta(before_pixels,after_pixels);
                evidence.lighting_response_pass = evidence.pixel_delta > .0001f && evidence.after_mean_luma > evidence.before_mean_luma &&
                    evidence.after_white_fraction < evidence.before_white_fraction+.03f;
            }
            finally
            {
                restore_light_state(actual);
                var restored = capture_light_state();
                evidence.lighting_restored = restored.ambient_mode == actual.ambient_mode && restored.shadow_mode == actual.shadow_mode &&
                    restored.sun == actual.sun && restored.sky == actual.sky && restored.equator == actual.equator && restored.ground == actual.ground && restored.fog == actual.fog &&
                    Mathf.Abs(restored.sun_intensity-actual.sun_intensity) < .00001f && Mathf.Abs(restored.shadow_strength-actual.shadow_strength) < .00001f &&
                    Vector3.Distance(restored.sun_euler,actual.sun_euler) < .001f;
                if(before) Destroy(before);
                if(after) Destroy(after);
            }
            evidence.technical_pass = evidence.full_inland_plane_pass && evidence.lighting_response_pass && evidence.lighting_restored;
            report.flat.neutral_comparison = evidence;
        }

        private NeutralComparisonReport measure_complete_inland_plane()
        {
            var preset = showcase.config.active_dark_layout;
            var evidence = new NeutralComparisonReport{cliff_masks=preset.fortified_city.cliffs.Length};
            var ground = showcase.ground;
            for(int z=0;z<=128;z++) for(int x=0;x<=128;x++)
            {
                float world_x = x*showcase.config.map_size/128;
                float world_z = z*showcase.config.map_size/128;
                if(world_x-ground.concept.coast_edge(world_z) < 4.05f || ground.concept.pond_edge_distance(world_x,world_z) < ground.concept.layout.pond_bank_width+.55f)
                {
                    evidence.water_or_bank_samples++;
                    continue;
                }
                evidence.inland_samples++;
                evidence.maximum_inland_height_error_m = Mathf.Max(evidence.maximum_inland_height_error_m,
                    Mathf.Abs(ground.sample_ground(world_x,world_z).y-preset.flat_height));
                evidence.maximum_inland_slope_degrees = Mathf.Max(evidence.maximum_inland_slope_degrees,
                    ground.terrain_data.GetSteepness(world_x/showcase.config.map_size,world_z/showcase.config.map_size));
            }
            evidence.full_inland_plane_pass = evidence.cliff_masks == 0 && evidence.inland_samples > 0 && evidence.maximum_inland_height_error_m < .003f;
            return evidence;
        }

        private LightState capture_light_state()
        {
            var sun = showcase.review_sun;
            return new LightState{ambient_mode=RenderSettings.ambientMode.ToString(),shadow_mode=sun.shadows.ToString(),sun=sun.color,sun_intensity=sun.intensity,
                shadow_strength=sun.shadowStrength,sun_euler=sun.transform.eulerAngles,ambient=RenderSettings.ambientLight,sky=RenderSettings.ambientSkyColor,
                equator=RenderSettings.ambientEquatorColor,ground=RenderSettings.ambientGroundColor,fog=RenderSettings.fogColor,ambient_intensity=RenderSettings.ambientIntensity};
        }

        private void apply_source_light(DarkWildernessConfig source)
        {
            RenderSettings.ambientMode = source.ambient_mode;
            RenderSettings.ambientLight = source.ambient;
            RenderSettings.ambientSkyColor = source.ambient_sky;
            RenderSettings.ambientEquatorColor = source.ambient_equator;
            RenderSettings.ambientGroundColor = source.ambient_ground;
            RenderSettings.fogColor = source.fog;
            showcase.review_sun.color = source.sun;
            showcase.review_sun.intensity = source.sun_intensity;
        }

        private void restore_light_state(LightState state)
        {
            RenderSettings.ambientMode = (UnityEngine.Rendering.AmbientMode)Enum.Parse(typeof(UnityEngine.Rendering.AmbientMode),state.ambient_mode);
            RenderSettings.ambientLight = state.ambient;
            RenderSettings.ambientSkyColor = state.sky;
            RenderSettings.ambientEquatorColor = state.equator;
            RenderSettings.ambientGroundColor = state.ground;
            RenderSettings.ambientIntensity = state.ambient_intensity;
            RenderSettings.fogColor = state.fog;
            var sun = showcase.review_sun;
            sun.color = state.sun;
            sun.intensity = state.sun_intensity;
            sun.shadowStrength = state.shadow_strength;
            sun.shadows = (LightShadows)Enum.Parse(typeof(LightShadows),state.shadow_mode);
        }

        private static void measure_light_pixels(Color32[] pixels,out float mean,out float black_fraction,out float white_fraction)
        {
            double total = 0;
            int black = 0,white = 0;
            foreach(var pixel in pixels)
            {
                total += (.2126*pixel.r+.7152*pixel.g+.0722*pixel.b)/255;
                if(pixel.r < 16 && pixel.g < 16 && pixel.b < 16) black++;
                if(pixel.r > 249 && pixel.g > 249 && pixel.b > 249) white++;
            }
            mean = (float)(total/pixels.Length);
            black_fraction = black/(float)pixels.Length;
            white_fraction = white/(float)pixels.Length;
        }

        [Serializable] private sealed class FortifiedCityReport
        {
            public string version,source,entrance_note,missing_art;
            public string source_seam_note;
            public string scope = "Independent environment composition only. No operable gate, gameplay collision, resource economy or NavMesh acceptance; art approval remains false.";
            public bool technical_pass,art_visual_accepted,wall_source_transform_pass,wall_scale_pass,wall_ground_pass;
            public bool composition_only,complete_city_claimed;
            public string city_fixture_scope;
            public bool fixture_manifest_pass,city_fixture_clearance_pass,patrol_points_pass,entry_clearance_pass,protected_flat_pass;
            public bool source_seams_pass,wall_perimeter_pass,wall_pixel_pass,cliff_geometry_pass,cliff_pixel_pass,cliff_material_pass;
            public bool cliff_checks_applicable;
            public string cliff_scope;
            public bool source_gate_leaf_present;
            public Vector2 city_center_m,outer_size_m,inner_size_m,portal_center_m;
            public Vector2 outer_center_offset_m,inner_center_offset_m;
            public float portal_clear_width_m,minimum_city_fixture_wall_gap_m,minimum_patrol_half_width_m,maximum_protected_height_error_m;
            public float wall_pixel_delta,wall_footing_error_m,source_max_seam_gap_m,source_max_footing_gap_m;
            public int wall_piece_bounds,perimeter_samples,unexpected_perimeter_gaps,entry_samples,blocked_entry_samples;
            public int protected_samples,city_fixture_count;
            public FortifiedCliffReport[] cliffs;
            public string[] failed_city_fixtures;
        }

        [Serializable] private sealed class FortifiedCliffReport
        {
            public string name,geometry_on,geometry_off,material_on,material_off;
            public float expected_top_m,actual_top_m,maximum_slope_degrees,geometry_pixel_delta,material_pixel_delta;
            public bool geometry_pass,geometry_pixel_pass,material_pass,heightfield_restored;
        }

        private IEnumerator run_fortified_city_checks()
        {
            var city = showcase.config.active_dark_layout.fortified_city;
            if(!city) yield break;
            var evidence = measure_fortified_city(city);
            showcase.set_focus(city.detail_focus[0],2);
            for(int frame=0;frame<40;frame++) yield return null;
            yield return new WaitForEndOfFrame();
            save_camera_image("fortified-city-overview.png");
            evidence.wall_pixel_delta = compare_same_frame_object(showcase.fortifications.instance,"fortified-wall");
            evidence.wall_pixel_pass = evidence.wall_pixel_delta > .001f;
            for(int index=0;index<city.detail_focus.Length;index++)
            {
                showcase.set_focus(city.detail_focus[index],index == 0 ? 1 : 0);
                for(int frame=0;frame<30;frame++) yield return null;
                yield return new WaitForEndOfFrame();
                save_camera_image("fortified-detail-"+index+".png");
            }
            var cliffs = new List<FortifiedCliffReport>();
            for(int index=0;index<city.cliffs.Length;index++)
            {
                var cliff = city.cliffs[index];
                showcase.set_focus(cliff.center_m/showcase.config.map_size,0);
                for(int frame=0;frame<30;frame++) yield return null;
                yield return new WaitForEndOfFrame();
                cliffs.Add(measure_fortified_cliff(city,cliff,index));
            }
            evidence.cliffs = cliffs.ToArray();
            evidence.cliff_checks_applicable = city.cliffs.Length > 0;
            evidence.cliff_scope = city.boundary_cliff_enabled ? "V6 connected non-activity north boundary cap and side returns; no slope across buildable/activity ground. Not the two isolated v4 cliff experiments." : evidence.cliff_checks_applicable ? "Two local non-walkable Terrain relief experiments, v4 only." : "No inland cliff height masks; all dry activity ground remains on the shared plane. Cliff geometry/material checks are not applicable, not passed art.";
            evidence.cliff_geometry_pass = evidence.cliffs.Length == 2;
            evidence.cliff_pixel_pass = evidence.cliff_material_pass = evidence.cliff_checks_applicable;
            foreach(var cliff in evidence.cliffs)
            {
                evidence.cliff_geometry_pass &= cliff.geometry_pass && cliff.heightfield_restored;
                evidence.cliff_pixel_pass &= cliff.geometry_pixel_pass;
                evidence.cliff_material_pass &= cliff.material_pass;
            }
            evidence.technical_pass = evidence.wall_source_transform_pass && evidence.wall_scale_pass && evidence.wall_ground_pass &&
                evidence.fixture_manifest_pass && evidence.city_fixture_clearance_pass && evidence.patrol_points_pass && evidence.entry_clearance_pass &&
                evidence.protected_flat_pass && evidence.source_seams_pass && evidence.wall_perimeter_pass && evidence.wall_pixel_pass &&
                (!evidence.cliff_checks_applicable || (evidence.cliff_geometry_pass && evidence.cliff_pixel_pass && evidence.cliff_material_pass)) && !evidence.source_gate_leaf_present;
            report.flat.fortified_city = evidence;
            showcase.set_view(0,1);
        }

        private FortifiedCityReport measure_fortified_city(VisionFortifiedConfig city)
        {
            var instance = showcase.fortifications.instance;
            var source = city.wall_source;
            var evidence = new FortifiedCityReport{version=city.version,source=city.source_record,entrance_note=city.entrance_note,missing_art=city.missing_art,
                city_center_m=city.city_center_m,outer_size_m=city.outer_size_m,inner_size_m=city.inner_size_m,portal_center_m=city.portal_center_m,
                outer_center_offset_m=city.outer_center_offset_m,inner_center_offset_m=city.inner_center_offset_m,
                portal_clear_width_m=city.portal_clear_width_m,source_gate_leaf_present=city.portal_has_gate_leaf,wall_piece_bounds=city.wall_footprints.Length,
                minimum_city_fixture_wall_gap_m=float.PositiveInfinity,minimum_patrol_half_width_m=float.PositiveInfinity,
                source_seams_pass=city.handoff_source_seams_pass,source_max_seam_gap_m=city.handoff_max_seam_gap_m,
                source_max_footing_gap_m=city.handoff_max_footing_gap_m,source_seam_note=city.handoff_seam_note};
            evidence.composition_only = city.composition_only;
            evidence.city_fixture_scope = city.dense_repetition ? "V6 user-authorized repeated source district: seven original-source roots inside the unchanged wall; the remaining repeated town/farm roots are outside. This boolean guards inside-wall clearance, not gameplay allegiance or economy. Damaged house geometry remains original." :
                city.composition_only ? "Three original functional source roots only, not a complete settlement/economy source set. Neutral damaged houses do not count as friendly housing." : "Original v4 experimental city fixture guard: at least five city roots, plus explicit expected manifest count; not proof of full economy/art readiness.";
            evidence.wall_source_transform_pass = compare_source_transforms(instance,source.prefab) &&
                Quaternion.Angle(instance.transform.rotation,source.prefab.transform.localRotation) < .0001f;
            evidence.wall_scale_pass = Vector3.Distance(instance.transform.localScale,source.prefab.transform.localScale) < .00001f;
            evidence.wall_footing_error_m = Mathf.Abs(instance.transform.position.y+source.native_min_y-showcase.config.active_dark_layout.flat_height);
            evidence.wall_ground_pass = evidence.wall_footing_error_m < .003f && instance.GetComponentsInChildren<Collider>(true).Length == 0;
            evidence.fixture_manifest_pass = showcase.landmarks.placements.Count == city.fixture_ids.Length;
            var failed = new List<string>();
            for(int index=0;index<showcase.landmarks.placements.Count;index++)
            {
                var placement = showcase.landmarks.placements[index];
                evidence.fixture_manifest_pass &= index < city.fixture_source_ids.Length && placement.source_id == city.fixture_source_ids[index];
                if(index >= city.human_city_fixtures.Length || !city.human_city_fixtures[index]) continue;
                evidence.city_fixture_count++;
                float gap = measure_city_fixture_wall_gap(city,index);
                evidence.minimum_city_fixture_wall_gap_m = Mathf.Min(evidence.minimum_city_fixture_wall_gap_m,gap);
                if(gap < city.patrol_clearance_m) failed.Add(city.fixture_ids[index]+": "+gap.ToString("F3")+"m to wall footprint");
            }
            evidence.failed_city_fixtures = failed.ToArray();
            evidence.city_fixture_clearance_pass = failed.Count == 0 && evidence.city_fixture_count == city.expected_city_fixture_count;
            if(!city.composition_only) evidence.city_fixture_clearance_pass &= evidence.city_fixture_count >= 5;
            foreach(var point in city.interior_clearance_points_m)
            {
                float half_width = Mathf.Min(city.measure_wall_edge(point.x,point.y),showcase.ground.concept.footprints.measure_native_source_edge(point.x,point.y));
                evidence.minimum_patrol_half_width_m = Mathf.Min(evidence.minimum_patrol_half_width_m,half_width);
            }
            evidence.patrol_points_pass = evidence.minimum_patrol_half_width_m >= city.patrol_clearance_m*.5f;
            measure_fortified_entry(city,evidence);
            measure_fortified_perimeter(city,evidence);
            foreach(var point in city.protected_flat_points_m)
            {
                evidence.protected_samples++;
                float height = showcase.ground.sample_ground(point.x,point.y).y;
                evidence.maximum_protected_height_error_m = Mathf.Max(evidence.maximum_protected_height_error_m,Mathf.Abs(height-showcase.config.active_dark_layout.flat_height));
            }
            evidence.protected_flat_pass = evidence.protected_samples > 0 && evidence.maximum_protected_height_error_m < .003f;
            return evidence;
        }

        private float measure_city_fixture_wall_gap(VisionFortifiedConfig city,int fixture)
        {
            var placement = showcase.landmarks.placements[fixture];
            var source = showcase.landmarks.placed_sources[fixture];
            var half = source.native_dimensions*placement.uniform_scale*.5f;
            var rotation = Quaternion.Euler(0,placement.yaw,0);
            float nearest = float.PositiveInfinity;
            for(int x=-1;x<=1;x++) for(int z=-1;z<=1;z++)
            {
                var offset = rotation*new Vector3(x*half.x,0,z*half.z);
                var point = placement.center*showcase.config.map_size+new Vector2(offset.x,offset.z);
                if(!city.contains_city(point.x,point.y)) return -1;
                nearest = Mathf.Min(nearest,city.measure_wall_edge(point.x,point.y));
            }
            return nearest;
        }

        private void measure_fortified_entry(VisionFortifiedConfig city,FortifiedCityReport evidence)
        {
            for(float z=city.portal_center_m.y-5;z<=city.portal_center_m.y+5;z+=.5f)
                for(int cross=-2;cross<=2;cross++)
                {
                    float x = city.portal_center_m.x+cross*(city.portal_clear_width_m*.5f-.2f)*.5f;
                    evidence.entry_samples++;
                    if(city.measure_wall_edge(x,z) < 0 || showcase.ground.concept.footprints.measure_native_source_edge(x,z) < 1 ||
                        Mathf.Abs(showcase.ground.sample_ground(x,z).y-showcase.config.active_dark_layout.flat_height) > .003f)
                        evidence.blocked_entry_samples++;
                }
            evidence.entry_clearance_pass = evidence.entry_samples > 0 && evidence.blocked_entry_samples == 0;
        }

        private static void measure_fortified_perimeter(VisionFortifiedConfig city,FortifiedCityReport evidence)
        {
            var half = (city.outer_size_m+city.inner_size_m)*.25f;
            for(int side=0;side<4;side++)
            {
                float length = side < 2 ? half.x*2 : half.y*2;
                int samples = Mathf.CeilToInt(length/.25f);
                for(int index=0;index<=samples;index++)
                {
                    float position = Mathf.Lerp(-1,1,index/(float)samples);
                    var point = city.city_center_m+(city.outer_center_offset_m+city.inner_center_offset_m)*.5f+
                        (side < 2 ? new Vector2(half.x*position,side == 0 ? -half.y : half.y) : new Vector2(side == 2 ? -half.x : half.x,half.y*position));
                    bool portal = side == 1 && Mathf.Abs(point.x-city.portal_center_m.x) < city.portal_clear_width_m*.5f;
                    evidence.perimeter_samples++;
                    if(!portal && city.measure_wall_edge(point.x,point.y) > .05f) evidence.unexpected_perimeter_gaps++;
                }
            }
            evidence.wall_perimeter_pass = evidence.perimeter_samples > 0 && evidence.unexpected_perimeter_gaps == 0;
        }

        private FortifiedCliffReport measure_fortified_cliff(VisionFortifiedConfig city,VisionFortifiedConfig.CliffRegion cliff,int index)
        {
            var ground = showcase.ground;
            var data = ground.terrain_data;
            var evidence = new FortifiedCliffReport{name=cliff.name,expected_top_m=cliff.top_height_m,actual_top_m=ground.sample_ground(cliff.center_m.x,cliff.center_m.y).y};
            for(int angle=0;angle<96;angle++)
            {
                float phase = angle*Mathf.PI*2/96;
                var point = cliff.center_m+new Vector2(Mathf.Cos(phase)*cliff.radius_m.x,Mathf.Sin(phase)*cliff.radius_m.y);
                evidence.maximum_slope_degrees = Mathf.Max(evidence.maximum_slope_degrees,data.GetSteepness(point.x/showcase.config.map_size,point.y/showcase.config.map_size));
            }
            evidence.geometry_pass = Mathf.Abs(evidence.actual_top_m-cliff.top_height_m) < .01f && evidence.maximum_slope_degrees > 45;
            var heights = data.GetHeights(0,0,data.heightmapResolution,data.heightmapResolution);
            var flattened = (float[,])heights.Clone();
            float size = showcase.config.map_size;
            for(int z=0;z<data.heightmapResolution;z++) for(int x=0;x<data.heightmapResolution;x++)
            {
                var point = new Vector2(x*size/(data.heightmapResolution-1),z*size/(data.heightmapResolution-1));
                if(cliff.measure_edge(point) < cliff.edge_width_m*.5f+.5f)
                    flattened[z,x] = showcase.config.active_dark_layout.flat_height/showcase.config.height_range;
            }
            Texture2D visible = null,hidden = null;
            try
            {
                visible = capture_camera();
                data.SetHeights(0,0,flattened);
                ground.terrain.Flush();
                hidden = capture_camera();
                evidence.geometry_on = "fortified-cliff-"+index+"-geometry-on.png";
                evidence.geometry_off = "fortified-cliff-"+index+"-geometry-off.png";
                File.WriteAllBytes(Path.Combine(output_directory,evidence.geometry_on),visible.EncodeToPNG());
                File.WriteAllBytes(Path.Combine(output_directory,evidence.geometry_off),hidden.EncodeToPNG());
                evidence.geometry_pixel_delta = measure_pixel_delta(visible.GetPixels32(),hidden.GetPixels32());
                evidence.geometry_pixel_pass = evidence.geometry_pixel_delta > .001f;
            }
            finally
            {
                data.SetHeights(0,0,heights);
                ground.terrain.Flush();
                evidence.heightfield_restored = Mathf.Abs(ground.sample_ground(cliff.center_m.x,cliff.center_m.y).y-evidence.actual_top_m) < .001f;
                if(visible) Destroy(visible);
                if(hidden) Destroy(hidden);
            }
            var material = ground.terrain.materialTemplate;
            float previous = material.GetFloat("_HasCliffPbr");
            try
            {
                visible = capture_camera();
                material.SetFloat("_HasCliffPbr",0);
                hidden = capture_camera();
                evidence.material_on = "fortified-cliff-"+index+"-pbr-on.png";
                evidence.material_off = "fortified-cliff-"+index+"-pbr-off.png";
                File.WriteAllBytes(Path.Combine(output_directory,evidence.material_on),visible.EncodeToPNG());
                File.WriteAllBytes(Path.Combine(output_directory,evidence.material_off),hidden.EncodeToPNG());
                evidence.material_pixel_delta = measure_pixel_delta(visible.GetPixels32(),hidden.GetPixels32());
                evidence.material_pass = previous == 1 && evidence.material_pixel_delta > .00001f;
            }
            finally
            {
                material.SetFloat("_HasCliffPbr",previous);
                if(visible) Destroy(visible);
                if(hidden) Destroy(hidden);
            }
            return evidence;
        }
    }
}
