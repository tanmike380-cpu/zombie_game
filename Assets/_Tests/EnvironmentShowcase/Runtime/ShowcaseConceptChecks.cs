using System;
using System.Collections;
using System.IO;
using UnityEngine;

namespace ZombieGame.Tests.EnvironmentShowcase
{
    public sealed partial class ShowcaseChecks
    {
        [Serializable] private sealed class ConceptReport
        {
            public string handoff, assumptions, missing_art;
            public bool no_river_pass, pool_closed_pass, road_absent_pass, water_pixel_pass, trees_pixel_pass;
            public float pool_depth_m, minimum_pool_rim_clearance_m, maximum_road_weight;
            public float sea_pixel_delta, pool_pixel_delta, trees_pixel_delta;
        }

        private IEnumerator run_concept_checks()
        {
            showcase.show_hud=false;
            report.biome="concept-05-07-09"; report.biome_label="Untouched wilderness technical preview: slopes + coast + pond";
            report.unavailable_art=showcase.missing_art;
            report.implemented=new[]{"Three image-derived spatial studies; finite 128m interpretation","Six PBR assets maintained; Road deliberately zero for user-requested untouched wilderness", "Rounded real grass hills",
                "Limestone coastal heightfield, no overhang", "Closed pond at independent water level", "Actual Terrain-depth water colour + low-cost analytic ripples", "Approved generic vegetation and small rocks, source LOD/instancing"};
            report.previous_approved_views="No wilderness Unity visual acceptance. Revised *-wilderness-v2 concept boards are source direction only; old routes/cliffs validated separately without relaxed expected heights.";
            report.layer_semantics=new[]{"Grass_A","Grass_Dry","Dirt: disconnected forest/slope soil","Mud at actual water edges","Stone on slopes","Road asset preserved, zero weight; no paths or wheel ruts"};
            report.concept=new ConceptReport{handoff=showcase.config.concept_layout.handoff_record,missing_art=showcase.missing_art,
                assumptions="2D reference composition interpreted into metres, not a measured heightmap. West sea; central rounded meadow; NE closed pond. Existing generic TreeSmall is not reference species."};
            report.run_in_background=Application.runInBackground; report.target_frame_rate=Application.targetFrameRate;
            report.shadow_distance=QualitySettings.shadowDistance; report.tree_shadow_draw_distance=showcase.config.tree_shadow_draw_distance;
            report.shadow_cascades=QualitySettings.shadowCascades==0?1:QualitySettings.shadowCascades;
            inspect_ground(); inspect_height_bounds(); inspect_concept_geometry();
            report.cliff_source=showcase.current_biome.cliff_source_record;
            bool is_style_check=Array.IndexOf(Environment.GetCommandLineArgs(),"-showcaseStyleCheck")>=0;
            for(int view=0;view<4;view++)
                for(int zoom=is_style_check?1:0;zoom<(is_style_check?2:3);zoom++)
                {
                    showcase.set_view(view,zoom);
                    for(int frame=0;frame<45;frame++) yield return null;
                    var samples=new float[is_style_check?30:180];
                    for(int frame=0;frame<samples.Length;frame++){yield return null;samples[frame]=Time.unscaledDeltaTime*1000;}
                    timings.Add(build_timing(samples,zoom,true,"concept-"+view+"-"+new[]{"near","middle","far"}[zoom]));
                    yield return new WaitForEndOfFrame();
                    save_camera_image("concept-"+view+"-"+zoom+".png");
                }
            showcase.set_view(0,0); yield return new WaitForEndOfFrame();
            report.terrain_mean_rgb_delta=compare_same_frame_terrain();
            report.terrain_pixel_pass=report.terrain_mean_rgb_delta>.006f;
            report.grass_mean_rgb_delta=compare_same_frame_meadow_grass(); report.grass_pixel_pass=report.grass_mean_rgb_delta>.0003f;
            report.rock_mean_rgb_delta=compare_same_frame_rocks();
            report.rock_pixel_pass=report.rock_instances>0&&report.rock_mean_rgb_delta>.00001f;
            showcase.set_view(1,0); yield return new WaitForEndOfFrame();
            report.concept.sea_pixel_delta=compare_same_frame_renderer(showcase.concept_water.sea,"concept-sea");
            report.cliff_material_mean_rgb_delta=compare_same_frame_material("_HasCliffPbr","concept-coast-pbr");
            report.cliff_material_pixel_pass=report.cliff_material_mean_rgb_delta>.0001f;
            showcase.set_view(2,0); yield return new WaitForEndOfFrame();
            report.concept.pool_pixel_delta=compare_same_frame_renderer(showcase.concept_water.pond,"concept-pool");
            report.concept.water_pixel_pass=report.concept.sea_pixel_delta>.001f&&report.concept.pool_pixel_delta>.001f;
            showcase.set_focus(showcase.config.concept_layout.forest_centers[0],0);
            yield return new WaitForEndOfFrame();
            report.concept.trees_pixel_delta=compare_same_frame_forest(); report.concept.trees_pixel_pass=report.concept.trees_pixel_delta>.001f;
            report.timings=timings.ToArray();
            report.terrain_passed=report.terrain_pixel_pass&&report.layer_pass&&report.shader_pass&&report.height_boundary_pass&&report.collider_height_pass&&
                report.actual_slope_pass&&report.cliff_material_pixel_pass&&report.concept.pool_closed_pass&&report.concept.road_absent_pass;
            report.source_complete=report.grass_available&&report.trees_available&&report.rock_instances>0&&report.shrub_instances>0&&
                !string.IsNullOrWhiteSpace(report.grass_source)&&!string.IsNullOrWhiteSpace(report.tree_source)&&!string.IsNullOrWhiteSpace(report.rock_source);
            report.passed=report.terrain_passed&&report.source_complete&&report.grass_pixel_pass&&report.rock_pixel_pass&&
                report.concept.no_river_pass&&report.concept.water_pixel_pass&&report.concept.trees_pixel_pass;
        }

        private void inspect_concept_geometry()
        {
            var ground=showcase.ground; var layout=ground.concept.layout; var evidence=report.concept;
            evidence.no_river_pass=!ground.routes.enabled&&showcase.landmarks.buildings.Count==0&&showcase.landmarks.ores.Count==0;
            evidence.pool_depth_m=layout.pond_level-ground.terrain_data.GetInterpolatedHeight(layout.pond_center.x,layout.pond_center.y);
            evidence.minimum_pool_rim_clearance_m=float.PositiveInfinity;
            for(int sample=0;sample<128;sample++)
            {
                float angle=sample*Mathf.PI*2/128;
                var radial=new Vector2(Mathf.Cos(angle)*layout.pond_radius.x,Mathf.Sin(angle)*layout.pond_radius.y);
                var center=layout.pond_center*showcase.config.map_size;
                float low=radial.magnitude,high=radial.magnitude+8;
                for(int step=0;step<16;step++)
                {
                    var probe=center+radial.normalized*((low+high)*.5f);
                    if(ground.concept.pond_edge_distance(probe.x,probe.y)<layout.pond_bank_width+.5f) low=(low+high)*.5f;
                    else high=(low+high)*.5f;
                }
                var rim=center+radial.normalized*((low+high)*.5f);
                float height=ground.terrain_data.GetInterpolatedHeight(rim.x/showcase.config.map_size,rim.y/showcase.config.map_size);
                evidence.minimum_pool_rim_clearance_m=Mathf.Min(evidence.minimum_pool_rim_clearance_m,height-layout.pond_level);
            }
            evidence.pool_closed_pass=evidence.pool_depth_m>.5f&&evidence.minimum_pool_rim_clearance_m>.2f&&
                layout.pond_center.x*showcase.config.map_size-layout.pond_radius.x>layout.coast_x+20;
            var weights=new float[6];
            for(int z=0;z<=128;z++) for(int x=0;x<=128;x++)
            {
                ground.sample_weights(x/128f,z/128f,weights);
                evidence.maximum_road_weight=Mathf.Max(evidence.maximum_road_weight,weights[5]);
            }
            evidence.road_absent_pass=evidence.maximum_road_weight==0;
        }
    }
}
