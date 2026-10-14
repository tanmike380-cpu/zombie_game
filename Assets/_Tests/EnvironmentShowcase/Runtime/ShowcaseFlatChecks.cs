using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace ZombieGame.Tests.EnvironmentShowcase
{
    public sealed partial class ShowcaseChecks
    {
        [Serializable] private sealed class FlatReport
        {
            public string scope = "Environment-only technical checks, not navigation/gameplay/battle or art acceptance.";
            public float walkable_height_m;
            public float maximum_walkable_height_delta_m;
            public float maximum_road_weight;
            public int walkable_samples;
            public bool level_ground_pass;
            public bool no_river;
            public bool pool_closed_pass;
            public float minimum_pool_rim_clearance_m;
            public bool technical_pass,art_visual_accepted,fixture_overlap_pass,water_pixel_pass,shrubs_pixel_pass;
            public float sea_pixel_delta,pond_pixel_delta,shrubs_pixel_delta,eligible_grass_area_m2,grass_instances_per_eligible_m2;
            public string density_note = "1m grid estimate of permitted vegetation area; instance count/area, not alpha coverage or per-cell uniformity.";
            public string[] overlap_pairs;
            public ShowcaseFoliage.VisibilityProbe vegetation_middle;
            public ShowcaseFoliage.GrassProbe[] shrub_bounds;
            public float[] visible_land_layer_mean_weights;
            public int visible_land_samples;
            public string layer_sample_note = "Whole-map means remain in layer_mean_weights; visible land means exclude actual submerged sea/pond samples, not low grass areas.";
            public float vision_shadow_pixel_delta;
            public bool vision_shadow_pixel_pass;
            public int active_prop_shadow_casters;
            public int prop_renderers_without_shadows;
            public string vision_shadow_resolution;
            public VisionClumpCheck[] vision_clumps;
            public bool vision_clumps_pass;
            public ShowcaseFoliage.SourceVisibilityProbe[] vision_oak_views;
            public float[] vision_oak_pixel_deltas;
            public bool vision_oak_pass;
            public FixedPlantFrame[] matched_plants_frames;
            public VisionWaterResponseCheck[] vision_water_response;
        }

        private IEnumerator run_flat_checks()
        {
            showcase.show_hud = false;
            report.biome = showcase.config.vision_enabled ? "vision-1" : "flat-v1";
            report.biome_label = showcase.config.vision_enabled ? "Vision-1 level wilderness technical preview; art acceptance false" : "Flat wilderness technical preview; art visual acceptance false";
            report.unavailable_art = showcase.missing_art;
            report.implemented = new[]{"One inland height; no separate building pads", "Compact source settlement and mineral clusters", "Local approved grass/shrub patches, unchanged instance budgets", "Sea and closed pond; six PBR slots with Road zero"};
            if(showcase.config.vision_enabled)
                report.implemented = new[]{"One inland height; no separate building pads", "Compact frozen-source settlement and mineral clusters", "6000 grass and 140 mid-layer plants redistributed into source-specific beds; not a forest", "Sea and closed pond; six PBR slots with Road zero"};
            inspect_ground();
            report.flat = measure_flat_geometry();
            report.run_in_background = Application.runInBackground;
            report.target_frame_rate = Application.targetFrameRate;
            report.shadow_distance = QualitySettings.shadowDistance;
            report.shadow_cascades = QualitySettings.shadowCascades==0?1:QualitySettings.shadowCascades;
            var matched_frames = capture_matched_plants_frames();
            while(matched_frames.MoveNext()) yield return matched_frames.Current;
            if(showcase.config.vision_enabled)
            {
                showcase.set_flat_comparison_view(1);
                for(int frame=0;frame<45;frame++) yield return null;
                yield return new WaitForEndOfFrame();
                save_camera_image("vision-1-matched-flat-v3-middle.png");
                report.flat.vision_shadow_pixel_delta = compare_same_frame_vision_shadows();
                report.flat.vision_shadow_pixel_pass = report.flat.vision_shadow_pixel_delta > .0001f;
                report.flat.vision_shadow_resolution = QualitySettings.shadowResolution.ToString();
                inspect_vision_shadow_casters(report.flat);
            }
            for(int view=0;view<4;view++)
            {
                for(int zoom=0;zoom<3;zoom++)
                {
                    showcase.set_view(view,zoom);
                    for(int frame=0;frame<45;frame++) yield return null;
                    var samples = new float[180];
                    for(int frame=0;frame<samples.Length;frame++)
                    {
                        yield return null;
                        samples[frame] = Time.unscaledDeltaTime*1000;
                    }
                    timings.Add(build_timing(samples,zoom,true,"flat-"+view+"-"+new[]{"near","middle","far"}[zoom]));
                    yield return new WaitForEndOfFrame();
                    save_camera_image("flat-"+new[]{"settlement","coast","pond","overview"}[view]+"-"+new[]{"near","middle","far"}[zoom]+".png");
                }
            }
            showcase.set_view(0,0);
            yield return new WaitForEndOfFrame();
            report.terrain_mean_rgb_delta = compare_same_frame_terrain("flat-terrain");
            report.terrain_pixel_pass = report.terrain_mean_rgb_delta>.006f;
            showcase.set_view(1,0);
            yield return new WaitForEndOfFrame();
            report.flat.sea_pixel_delta = compare_same_frame_renderer(showcase.concept_water.sea,"flat-sea");
            showcase.set_view(2,0);
            yield return new WaitForEndOfFrame();
            report.flat.pond_pixel_delta = compare_same_frame_renderer(showcase.concept_water.pond,"flat-pond");
            report.flat.water_pixel_pass = report.flat.sea_pixel_delta>.001f && report.flat.pond_pixel_delta>.001f;
            if(showcase.config.vision_enabled)
            {
                var water_checks = capture_vision_water_response();
                while(water_checks.MoveNext()) yield return water_checks.Current;
            }
            showcase.set_focus(showcase.config.active_dark_layout.vegetation_centers[0],1);
            for(int frame=0;frame<30;frame++) yield return null;
            yield return new WaitForEndOfFrame();
            save_camera_image("flat-plants-middle.png");
            report.flat.vegetation_middle = showcase.foliage.sample_visibility(showcase.review_camera);
            showcase.set_focus(showcase.config.active_dark_layout.vegetation_centers[0],0);
            yield return new WaitForEndOfFrame();
            save_camera_image("flat-plants-near.png");
            report.grass_mean_rgb_delta = compare_same_frame_meadow_grass();
            report.grass_pixel_pass = report.grass_mean_rgb_delta>.0003f;
            report.flat.shrubs_pixel_delta = compare_same_frame_shrubs();
            report.flat.shrubs_pixel_pass = report.flat.shrubs_pixel_delta>.00001f;
            report.flat.shrub_bounds = showcase.foliage.shrub_probes.ToArray();
            var source_checks = sample_dark_sources();
            while(source_checks.MoveNext()) yield return source_checks.Current;
            if(showcase.config.vision_enabled)
            {
                var clump_checks = run_vision_clump_checks();
                while(clump_checks.MoveNext()) yield return clump_checks.Current;
                if(showcase.config.active_dark_layout.tree_prefabs.Length > 0)
                {
                    var oak_checks = run_vision_oak_checks();
                    while(oak_checks.MoveNext()) yield return oak_checks.Current;
                }
            }
            report.timings = timings.ToArray();
            report.terrain_passed = report.terrain_pixel_pass && report.layer_pass && report.shader_pass && report.flat.level_ground_pass && report.flat.maximum_road_weight==0 && report.flat.no_river && report.flat.pool_closed_pass && report.flat.water_pixel_pass;
            report.flat.technical_pass = report.terrain_passed && report.dark_source.passed && report.flat.fixture_overlap_pass && report.grass_pixel_pass && report.flat.shrubs_pixel_pass;
            if(showcase.config.vision_enabled) report.flat.technical_pass &= report.flat.vision_shadow_pixel_pass && report.flat.vision_clumps_pass;
            if(showcase.config.vision_enabled && showcase.config.active_dark_layout.tree_prefabs.Length > 0)
                report.flat.technical_pass &= report.flat.vision_oak_pass;
            report.passed = report.flat.technical_pass;
            report.source_complete = false;
            report.error = report.passed ? "Technical checks passed; art_visual_accepted=false. Dense tree crowns, broader RTS-readable grass clumps, reeds and long authored cliff modules unavailable. No gameplay acceptance." : "Flat technical check failed; inspect individual flags. Art not accepted.";
        }

        private FlatReport measure_flat_geometry()
        {
            var ground = showcase.ground;
            var evidence = new FlatReport{walkable_height_m=showcase.config.active_dark_layout.flat_height,no_river=!ground.routes.enabled};
            if(showcase.config.vision_enabled)
                evidence.density_note = "Legacy vegetation-area comparison retained, not the new per-source bed density. See vision_clumps nominal/eligible support areas; neither is alpha coverage.";
            var weights = new float[6];
            for(int z=0;z<=128;z++)
            {
                for(int x=0;x<=128;x++)
                {
                    float u = x/128f;
                    float v = z/128f;
                    ground.sample_weights(u,v,weights);
                    evidence.maximum_road_weight = Mathf.Max(evidence.maximum_road_weight,weights[5]);
                    float world_x = u*showcase.config.map_size;
                    float world_z = v*showcase.config.map_size;
                    if(world_x-ground.concept.coast_edge(world_z)<7 || ground.concept.pond_edge_distance(world_x,world_z)<ground.concept.layout.pond_bank_width+1) continue;
                    evidence.walkable_samples++;
                    if(ground.concept.can_place(world_x,world_z) && ground.concept.vegetation_weight(world_x,world_z)>.45f && ground.concept.exposed_soil(world_x,world_z)<.35f)
                        evidence.eligible_grass_area_m2 += showcase.config.map_size*showcase.config.map_size/(128*128);
                    float height = ground.sample_ground(world_x,world_z).y;
                    evidence.maximum_walkable_height_delta_m = Mathf.Max(evidence.maximum_walkable_height_delta_m,Mathf.Abs(height-evidence.walkable_height_m));
                }
            }
            evidence.level_ground_pass = evidence.walkable_samples>0 && evidence.maximum_walkable_height_delta_m<.003f;
            evidence.minimum_pool_rim_clearance_m = measure_flat_pool_rim();
            evidence.pool_closed_pass = evidence.minimum_pool_rim_clearance_m>.25f;
            evidence.grass_instances_per_eligible_m2 = showcase.foliage.grass_instances/Mathf.Max(1,evidence.eligible_grass_area_m2);
            evidence.overlap_pairs = find_flat_fixture_overlaps();
            evidence.fixture_overlap_pass = evidence.overlap_pairs.Length==0;
            if(showcase.config.vision_enabled) measure_visible_land_layers(evidence);
            return evidence;
        }

        private void measure_visible_land_layers(FlatReport evidence)
        {
            var ground = showcase.ground;
            var weights = new float[6];
            evidence.visible_land_layer_mean_weights = new float[6];
            const int SAMPLE_GRID = 40;
            for(int z=0;z<SAMPLE_GRID;z++)
            {
                for(int x=0;x<SAMPLE_GRID;x++)
                {
                    float u = x/(float)(SAMPLE_GRID-1);
                    float v = z/(float)(SAMPLE_GRID-1);
                    float world_x = u*showcase.config.map_size;
                    float world_z = v*showcase.config.map_size;
                    float height = ground.sample_ground(world_x,world_z).y;
                    bool sea_submerged = height < ground.concept.layout.sea_level && world_x <= ground.concept.coast_edge(world_z)+3.5f;
                    bool pond_submerged = height < ground.concept.layout.pond_level && ground.concept.pond_edge_distance(world_x,world_z)<ground.concept.layout.pond_bank_width;
                    if(sea_submerged || pond_submerged) continue;
                    ground.sample_weights(u,v,weights);
                    evidence.visible_land_samples++;
                    for(int index=0;index<6;index++) evidence.visible_land_layer_mean_weights[index] += weights[index];
                }
            }
            for(int index=0;index<6;index++) evidence.visible_land_layer_mean_weights[index] /= Mathf.Max(1,evidence.visible_land_samples);
        }

        private void inspect_vision_shadow_casters(FlatReport evidence)
        {
            foreach(var fixture in showcase.landmarks.buildings)
            {
                foreach(var renderer in fixture.GetComponentsInChildren<MeshRenderer>())
                {
                    if(!renderer.enabled) continue;
                    if(renderer.shadowCastingMode == UnityEngine.Rendering.ShadowCastingMode.Off)
                        evidence.prop_renderers_without_shadows++;
                    else evidence.active_prop_shadow_casters++;
                }
            }
        }

        private float compare_same_frame_vision_shadows()
        {
            var light = showcase.review_sun;
            var previous = light.shadows;
            Texture2D visible = null;
            Texture2D hidden = null;
            try
            {
                visible = capture_camera();
                light.shadows = LightShadows.None;
                hidden = capture_camera();
                File.WriteAllBytes(Path.Combine(output_directory,"vision-shadow-on.png"),visible.EncodeToPNG());
                File.WriteAllBytes(Path.Combine(output_directory,"vision-shadow-off.png"),hidden.EncodeToPNG());
                return measure_pixel_delta(visible.GetPixels32(),hidden.GetPixels32());
            }
            finally
            {
                light.shadows = previous;
                if(visible) Destroy(visible);
                if(hidden) Destroy(hidden);
            }
        }

        private float measure_flat_pool_rim()
        {
            var ground = showcase.ground;
            var layout = ground.concept.layout;
            float minimum = float.PositiveInfinity;
            for(int index=0;index<128;index++)
            {
                float angle = index*Mathf.PI*2/128;
                var offset = new Vector2(Mathf.Cos(angle)*layout.pond_radius.x,Mathf.Sin(angle)*layout.pond_radius.y);
                var point = layout.pond_center*showcase.config.map_size+offset+offset.normalized*(layout.pond_bank_width+2);
                minimum = Mathf.Min(minimum,ground.sample_ground(point.x,point.y).y-layout.pond_level);
            }
            return minimum;
        }

        private string[] find_flat_fixture_overlaps()
        {
            var overlaps = new List<string>();
            var placements = showcase.landmarks.placements;
            for(int first=0;first<placements.Count;first++)
            {
                for(int second=first+1;second<placements.Count;second++)
                {
                    if(flat_boxes_overlap(first,second)) overlaps.Add(placements[first].source_id+"["+first+"] / "+placements[second].source_id+"["+second+"]");
                }
            }
            return overlaps.ToArray();
        }

        /// <summary>2D separating-axis check of exact source-oriented bounds at the whole uniform placement scale.</summary>
        private bool flat_boxes_overlap(int first,int second)
        {
            var placements = showcase.landmarks.placements;
            var sources = showcase.landmarks.placed_sources;
            var left_rotation = Quaternion.Euler(0,placements[first].yaw,0);
            var right_rotation = Quaternion.Euler(0,placements[second].yaw,0);
            var left_x = left_rotation*Vector3.right;
            var left_z = left_rotation*Vector3.forward;
            var right_x = right_rotation*Vector3.right;
            var right_z = right_rotation*Vector3.forward;
            var delta2 = (placements[first].center-placements[second].center)*showcase.config.map_size;
            var delta = new Vector3(delta2.x,0,delta2.y);
            var left_half = sources[first].native_dimensions*placements[first].uniform_scale*.5f;
            var right_half = sources[second].native_dimensions*placements[second].uniform_scale*.5f;
            foreach(var axis in new[]{left_x,left_z,right_x,right_z})
            {
                float left_radius = Mathf.Abs(Vector3.Dot(left_x,axis))*left_half.x+Mathf.Abs(Vector3.Dot(left_z,axis))*left_half.z;
                float right_radius = Mathf.Abs(Vector3.Dot(right_x,axis))*right_half.x+Mathf.Abs(Vector3.Dot(right_z,axis))*right_half.z;
                if(Mathf.Abs(Vector3.Dot(delta,axis))>=left_radius+right_radius) return false;
            }
            return true;
        }

        private float compare_same_frame_shrubs()
        {
            bool previous = showcase.foliage.show_shrubs;
            Texture2D visible = null,hidden = null;
            try
            {
                showcase.foliage.show_shrubs = true;
                visible = capture_camera();
                showcase.foliage.show_shrubs = false;
                hidden = capture_camera();
                File.WriteAllBytes(Path.Combine(output_directory,"flat-shrubs-on.png"),visible.EncodeToPNG());
                File.WriteAllBytes(Path.Combine(output_directory,"flat-shrubs-off.png"),hidden.EncodeToPNG());
                return measure_pixel_delta(visible.GetPixels32(),hidden.GetPixels32());
            }
            finally
            {
                showcase.foliage.show_shrubs = previous;
                if(visible) Destroy(visible);
                if(hidden) Destroy(hidden);
            }
        }
    }
}
