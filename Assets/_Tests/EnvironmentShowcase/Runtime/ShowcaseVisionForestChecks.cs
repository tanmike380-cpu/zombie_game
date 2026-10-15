using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ZombieGame.Tests.EnvironmentShowcase
{
    public sealed partial class ShowcaseChecks
    {
        [Serializable] private sealed class VisionForestCheck
        {
            public string version;
            public int tree_budget,actual_trees,dry_land_samples,forest_core_samples;
            public int largest_forest_component_samples,open_land_samples,canopy_envelope_samples;
            public int corridor_targets,connected_targets;
            public float sample_grid_m = 2;
            public float forest_core_area_m2,canopy_envelope_area_m2,minimum_corridor_canopy_clearance_m;
            public float tree_spacing_m;
            public float near_lod_screen_height,far_cull_screen_height;
            public bool budget_pass,corridor_clearance_pass,clearings_connected_pass,forest_mass_pass;
            public bool technical_pass;
            public bool navigation_integrated = false;
            public bool art_visual_accepted = false;
            public string scope = "Art-only tree/clearing arrangement. Raster connectivity is not NavMesh/pathfinding acceptance. Crown circles are conservative presentation envelopes, not measured alpha canopy cover.";
            public ShowcaseFoliage.SourceVisibilityProbe[] submitted_views;
            public float[] pixel_deltas;
            public VisionCorridorConnection[] connections;
            public bool source_lod_resolution_pass,near_lod0_visible,far_lod1_visible;
            public ShowcaseFoliage.TreePlacementProbe actual_tree_placement;
            public Vector2[] actual_tree_positions;
            public float minimum_native_fixture_canopy_clearance_m;
            public bool native_fixture_canopy_clearance_pass;
        }

        [Serializable] private sealed class VisionCorridorConnection
        {
            public string name;
            public Vector2 start_metres,end_metres;
            public int centerline_samples,nonflat_samples,source_footprint_samples;
            public int band_samples,nonflat_band_samples,source_footprint_band_samples;
            public Vector2 first_nonflat_point,first_source_footprint_point;
            public Vector2 first_nonflat_band_point,first_source_footprint_band_point;
            public float end_height_m,nearest_open_cell_distance_m;
            public bool start_dry,end_dry,target_found,reached_from_start;
        }

        private IEnumerator run_vision_forest_checks()
        {
            var forest = showcase.config.active_dark_layout.vision_forest;
            if(!forest) yield break;
            var check = measure_vision_forest(forest);
            var submissions = new List<ShowcaseFoliage.SourceVisibilityProbe>();
            var deltas = new List<float>();
            for(int zoom=0;zoom<3;zoom++)
            {
                showcase.set_focus(forest.review_focus[0],zoom);
                for(int frame=0;frame<45;frame++) yield return null;
                var samples = new float[180];
                for(int frame=0;frame<samples.Length;frame++)
                {
                    yield return null;
                    samples[frame] = Time.unscaledDeltaTime*1000;
                }
                string view = "connected-forest-"+new[]{"near","middle","far"}[zoom];
                timings.Add(build_timing(samples,zoom,true,view));
                yield return new WaitForEndOfFrame();
                save_camera_image("vision-"+view+".png");
                submissions.Add(showcase.foliage.sample_source_visibility(showcase.review_camera,"OakLauris71"));
                deltas.Add(compare_same_frame_vision_source("OakLauris71",view));
            }
            for(int index=1;index<forest.review_focus.Length;index++)
            {
                showcase.set_focus(forest.review_focus[index],1);
                for(int frame=0;frame<30;frame++) yield return null;
                yield return new WaitForEndOfFrame();
                save_camera_image("vision-connected-forest-region-"+index+".png");
            }
            showcase.set_focus(new Vector2(.62f,.52f),2);
            float previous_size = showcase.review_camera.orthographicSize;
            try
            {
                showcase.review_camera.orthographicSize = 44;
                for(int frame=0;frame<30;frame++) yield return null;
                yield return new WaitForEndOfFrame();
                save_camera_image("vision-connected-forest-overview.png");
            }
            finally
            {
                showcase.review_camera.orthographicSize = previous_size;
                showcase.set_view(0,1);
            }
            check.submitted_views = submissions.ToArray();
            check.pixel_deltas = deltas.ToArray();
            foreach(float delta in deltas) check.technical_pass &= delta > .000001f;
            check.source_lod_resolution_pass = true;
            foreach(var view in submissions)
                check.source_lod_resolution_pass &= view.source_unresolved_lod_parts == 0 && view.source_lod0_parts == 36 && view.source_lod1_parts == 36 &&
                    view.source_lod0_triangles == 5303 && view.source_lod1_triangles == 842 && view.source_triangles == 6145 && view.duplicate_lod_tree_origins == 0;
            check.near_lod0_visible = submissions[0].submitted_lod0_matrix_submeshes > 0;
            check.far_lod1_visible = submissions[2].submitted_lod1_matrix_submeshes > 0;
            check.technical_pass &= check.source_lod_resolution_pass && check.near_lod0_visible && check.far_lod1_visible;
            report.flat.vision_forest = check;
        }

        private VisionForestCheck measure_vision_forest(VisionForestConfig forest)
        {
            const int RESOLUTION = 64;
            float size = showcase.config.map_size;
            float step = size/RESOLUTION;
            var open_cells = new bool[RESOLUTION*RESOLUTION];
            var forest_cells = new bool[open_cells.Length];
            var check = new VisionForestCheck
            {
                version=forest.version,tree_budget=forest.tree_budget,
                actual_trees=showcase.foliage.tree_positions.Count,tree_spacing_m=forest.tree_spacing,
                near_lod_screen_height=forest.near_lod_screen_height,far_cull_screen_height=forest.far_cull_screen_height,
                sample_grid_m=step,minimum_corridor_canopy_clearance_m=float.PositiveInfinity,
                actual_tree_placement=showcase.foliage.tree_placement_probe,
                actual_tree_positions=showcase.foliage.tree_positions.ToArray(),
                minimum_native_fixture_canopy_clearance_m=float.PositiveInfinity
            };
            foreach(var tree in showcase.foliage.tree_positions)
            {
                check.minimum_corridor_canopy_clearance_m = Mathf.Min(check.minimum_corridor_canopy_clearance_m,
                    forest.measure_corridor_edge(tree,size)-forest.canopy_clearance);
                check.minimum_native_fixture_canopy_clearance_m = Mathf.Min(check.minimum_native_fixture_canopy_clearance_m,
                    showcase.ground.concept.footprints.measure_native_source_edge(tree.x,tree.y)-forest.canopy_clearance);
            }
            for(int z=0;z<RESOLUTION;z++)
            {
                for(int x=0;x<RESOLUTION;x++)
                {
                    var point = new Vector2((x+.5f)*step,(z+.5f)*step);
                    if(!is_vision_dry_point(point)) continue;
                    check.dry_land_samples++;
                    float weight = showcase.ground.concept.forest_weight(point.x,point.y);
                    int index = z*RESOLUTION+x;
                    forest_cells[index] = weight > .75f;
                    if(forest_cells[index]) check.forest_core_samples++;
                    bool canopy = false;
                    foreach(var tree in showcase.foliage.tree_positions)
                    {
                        if((point-tree).sqrMagnitude >= forest.canopy_clearance*forest.canopy_clearance) continue;
                        canopy = true;
                        break;
                    }
                    if(canopy) check.canopy_envelope_samples++;
                    open_cells[index] = weight < .15f && !canopy;
                    if(open_cells[index]) check.open_land_samples++;
                }
            }
            check.forest_core_area_m2 = check.forest_core_samples*step*step;
            check.canopy_envelope_area_m2 = check.canopy_envelope_samples*step*step;
            check.largest_forest_component_samples = measure_largest_component(forest_cells,RESOLUTION);
            measure_clearing_connections(forest,open_cells,RESOLUTION,step,check);
            check.budget_pass = check.actual_trees > 40 && check.actual_trees <= forest.tree_budget;
            check.corridor_clearance_pass = check.minimum_corridor_canopy_clearance_m >= 0;
            check.native_fixture_canopy_clearance_pass = check.minimum_native_fixture_canopy_clearance_m >= 0;
            check.clearings_connected_pass = check.connected_targets == check.corridor_targets;
            foreach(var connection in check.connections)
            {
                check.clearings_connected_pass &= connection.start_dry && connection.end_dry && connection.nonflat_samples == 0 && connection.source_footprint_samples == 0;
                if(forest.use_native_source_clearance)
                    check.clearings_connected_pass &= connection.nonflat_band_samples == 0 && connection.source_footprint_band_samples == 0;
            }
            check.forest_mass_pass = check.forest_core_area_m2 >= 1500 &&
                check.largest_forest_component_samples >= check.forest_core_samples*.20f;
            check.technical_pass = check.budget_pass && check.corridor_clearance_pass && check.clearings_connected_pass && check.forest_mass_pass;
            if(forest.use_native_source_clearance) check.technical_pass &= check.native_fixture_canopy_clearance_pass;
            return check;
        }

        private bool is_vision_dry_point(Vector2 point)
        {
            var ground = showcase.ground;
            return Mathf.Abs(ground.sample_authored_height(point.x,point.y)-showcase.config.active_dark_layout.flat_height) < .003f &&
                !(ground.concept.footprints?.contains(point.x,point.y) ?? false) &&
                (!showcase.config.active_dark_layout.fortified_city || showcase.config.active_dark_layout.fortified_city.measure_wall_edge(point.x,point.y) > 0);
        }

        private static int measure_largest_component(bool[] cells,int resolution)
        {
            var remaining = (bool[])cells.Clone();
            int largest = 0;
            for(int index=0;index<remaining.Length;index++)
            {
                if(!remaining[index]) continue;
                largest = Mathf.Max(largest,flood_cells(remaining,resolution,index,null));
            }
            return largest;
        }

        private void measure_clearing_connections(VisionForestConfig forest,bool[] cells,int resolution,float step,VisionForestCheck check)
        {
            int seed = find_open_cell(forest.corridors[0].points[0],cells,resolution,step);
            var connected = new bool[cells.Length];
            if(seed >= 0) flood_cells((bool[])cells.Clone(),resolution,seed,connected);
            var connections = new List<VisionCorridorConnection>();
            foreach(var corridor in forest.corridors)
            {
                check.corridor_targets++;
                int target = find_open_cell(corridor.points[corridor.points.Length-1],cells,resolution,step);
                if(target >= 0 && connected[target]) check.connected_targets++;
                var connection = measure_corridor_centerline(corridor);
                connection.target_found = target >= 0;
                connection.reached_from_start = target >= 0 && connected[target];
                connection.nearest_open_cell_distance_m = target < 0 ? -1 : Vector2.Distance(connection.end_metres,
                    new Vector2((target%resolution+.5f)*step,(target/resolution+.5f)*step));
                connections.Add(connection);
            }
            check.connections = connections.ToArray();
        }

        private VisionCorridorConnection measure_corridor_centerline(VisionForestConfig.Corridor corridor)
        {
            float size = showcase.config.map_size;
            var check = new VisionCorridorConnection
            {
                name=corridor.name,start_metres=corridor.points[0]*size,
                end_metres=corridor.points[corridor.points.Length-1]*size
            };
            check.start_dry = is_vision_dry_point(check.start_metres);
            check.end_dry = is_vision_dry_point(check.end_metres);
            check.end_height_m = showcase.ground.sample_authored_height(check.end_metres.x,check.end_metres.y);
            for(int segment=1;segment<corridor.points.Length;segment++)
            {
                var start = corridor.points[segment-1]*size;
                var end = corridor.points[segment]*size;
                var direction = (end-start).normalized;
                var normal = new Vector2(-direction.y,direction.x);
                int samples = Mathf.Max(1,Mathf.CeilToInt(Vector2.Distance(start,end)/.5f));
                for(int index=0;index<=samples;index++)
                {
                    var point = Vector2.Lerp(start,end,index/(float)samples);
                    record_corridor_sample(check,point,false);
                    if(showcase.config.active_dark_layout.vision_forest.use_native_source_clearance)
                    {
                        for(int cross=-2;cross<=2;cross++)
                            record_corridor_sample(check,point+normal*(cross*.5f*corridor.half_width),true);
                    }
                }
            }
            return check;
        }

        private void record_corridor_sample(VisionCorridorConnection check,Vector2 point,bool band)
        {
            bool nonflat = Mathf.Abs(showcase.ground.sample_authored_height(point.x,point.y)-showcase.config.active_dark_layout.flat_height) >= .003f;
            bool footprint = showcase.ground.concept.footprints?.contains(point.x,point.y) ?? false;
            var city = showcase.config.active_dark_layout.fortified_city;
            if(city) footprint |= city.measure_wall_edge(point.x,point.y) <= 0;
            if(band)
            {
                check.band_samples++;
                if(nonflat)
                {
                    if(check.nonflat_band_samples == 0) check.first_nonflat_band_point = point;
                    check.nonflat_band_samples++;
                }
                if(footprint)
                {
                    if(check.source_footprint_band_samples == 0) check.first_source_footprint_band_point = point;
                    check.source_footprint_band_samples++;
                }
                return;
            }
            check.centerline_samples++;
            if(nonflat)
            {
                if(check.nonflat_samples == 0) check.first_nonflat_point = point;
                check.nonflat_samples++;
            }
            if(footprint)
            {
                if(check.source_footprint_samples == 0) check.first_source_footprint_point = point;
                check.source_footprint_samples++;
            }
        }

        private static int find_open_cell(Vector2 normalized,bool[] cells,int resolution,float step)
        {
            var target = normalized*resolution-Vector2.one*.5f;
            float nearest = float.PositiveInfinity;
            int chosen = -1;
            for(int index=0;index<cells.Length;index++)
            {
                if(!cells[index]) continue;
                float distance = (new Vector2(index%resolution,index/resolution)-target).sqrMagnitude;
                if(distance >= nearest || distance*step*step > 16) continue;
                nearest = distance;
                chosen = index;
            }
            return chosen;
        }

        private static int flood_cells(bool[] remaining,int resolution,int seed,bool[] visited)
        {
            var queue = new Queue<int>();
            queue.Enqueue(seed);
            remaining[seed] = false;
            int count = 0;
            while(queue.Count > 0)
            {
                int index = queue.Dequeue();
                count++;
                if(visited != null) visited[index] = true;
                int x = index%resolution;
                int z = index/resolution;
                if(x > 0) enqueue_cell(index-1,remaining,queue);
                if(x+1 < resolution) enqueue_cell(index+1,remaining,queue);
                if(z > 0) enqueue_cell(index-resolution,remaining,queue);
                if(z+1 < resolution) enqueue_cell(index+resolution,remaining,queue);
            }
            return count;
        }

        private static void enqueue_cell(int index,bool[] remaining,Queue<int> queue)
        {
            if(!remaining[index]) return;
            remaining[index] = false;
            queue.Enqueue(index);
        }
    }
}
