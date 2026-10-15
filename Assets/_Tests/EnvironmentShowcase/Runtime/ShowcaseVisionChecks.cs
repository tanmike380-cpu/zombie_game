using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace ZombieGame.Tests.EnvironmentShowcase
{
    public sealed partial class ShowcaseChecks
    {
        [Serializable] private sealed class VisionWaterResponseCheck
        {
            public string surface,before_screenshot,after_screenshot;
            public float mean_rgb_delta,orthographic_size;
            public Vector3 camera_position,camera_euler;
            public bool response_restored;
            public string note = "Same-frame surface2 versus Vision water response. Existing Mud projected with actual heightfield attenuation, not true refraction; local Fresnel sky approximation, not scene reflection. Pixel contribution is not art acceptance.";
        }

        private IEnumerator capture_vision_water_response()
        {
            var evidence = new List<VisionWaterResponseCheck>();
            for(int view=1;view<=2;view++)
            {
                showcase.set_view(view,0);
                for(int frame=0;frame<30;frame++) yield return null;
                yield return new WaitForEndOfFrame();
                var renderer = view == 1 ? showcase.concept_water.sea : showcase.concept_water.pond;
                evidence.Add(compare_vision_water_response(renderer,view == 1 ? "coast" : "pond"));
            }
            report.flat.vision_water_response = evidence.ToArray();
        }

        private VisionWaterResponseCheck compare_vision_water_response(Renderer renderer,string surface)
        {
            var material = renderer.sharedMaterial;
            float previous = material.GetFloat("_VisionResponse");
            var camera = showcase.review_camera;
            var check = new VisionWaterResponseCheck
            {
                surface=surface,
                camera_position=camera.transform.position,
                camera_euler=camera.transform.eulerAngles,
                orthographic_size=camera.orthographicSize,
                before_screenshot="vision-water-"+surface+"-surface2.png",
                after_screenshot="vision-water-"+surface+"-response.png"
            };
            Texture2D before = null;
            Texture2D after = null;
            try
            {
                material.SetFloat("_VisionResponse",0);
                before = capture_camera();
                material.SetFloat("_VisionResponse",1);
                after = capture_camera();
                File.WriteAllBytes(Path.Combine(output_directory,check.before_screenshot),before.EncodeToPNG());
                File.WriteAllBytes(Path.Combine(output_directory,check.after_screenshot),after.EncodeToPNG());
                check.mean_rgb_delta = measure_pixel_delta(before.GetPixels32(),after.GetPixels32());
            }
            finally
            {
                material.SetFloat("_VisionResponse",previous);
                check.response_restored = material.GetFloat("_VisionResponse") == previous;
                if(before) Destroy(before);
                if(after) Destroy(after);
            }
            return check;
        }

        [Serializable] private sealed class FixedPlantFrame
        {
            public string screenshot;
            public Vector2 normalized_focus = new Vector2(.43f,.59f);
            public Vector3 camera_position,camera_euler;
            public float orthographic_size,aspect;
            public bool acceptance_camera = false;
            public string note = "Original flat-v3 plant focus/projection; diagnostic only. Existing layout-specific grass acceptance capture/threshold unchanged.";
        }

        private IEnumerator capture_matched_plants_frames()
        {
            var frames = new List<FixedPlantFrame>();
            for(int zoom=0;zoom<=1;zoom++)
            {
                showcase.set_flat_plants_comparison_view(zoom);
                for(int frame=0;frame<30;frame++) yield return null;
                yield return new WaitForEndOfFrame();
                string path = "matched-flat-v3-plants-"+(zoom == 0 ? "near" : "middle")+".png";
                save_camera_image(path);
                var camera = showcase.review_camera;
                frames.Add(new FixedPlantFrame{screenshot=path,camera_position=camera.transform.position,
                    camera_euler=camera.transform.eulerAngles,orthographic_size=camera.orthographicSize,aspect=camera.aspect});
            }
            report.flat.matched_plants_frames = frames.ToArray();
            showcase.set_view(0,1);
        }

        [Serializable] private sealed class VisionClumpCheck
        {
            public string source;
            public Vector2 sampled_focus;
            public ShowcaseFoliage.SourceVisibilityProbe near,middle;
            public float near_pixel_delta,middle_pixel_delta;
            public bool near_pixel_pass,middle_pixel_pass,source_geometry_pass;
            public int configured_instances;
            public float nominal_bed_ellipse_area_m2,eligible_bed_1m_grid_area_m2;
            public string area_note = "Nominal ellipse sum and 1m eligible-support estimate (mask>.2, source-safe dry land), not alpha cover; only the existing fixed budget was redistributed.";
            public ShowcaseFoliage.SourceGroundProbe exact_ground;
            public float grass_near_no_coverage_delta,grass_near_zero_mip_bias_delta;
        }

        private IEnumerator run_vision_clump_checks()
        {
            var evidence = new List<VisionClumpCheck>();
            var expected = new Dictionary<string,int>{{"Fern02A",784},{"Fern02B",2384},{"GrassMedium02E",1664}};
            foreach(var source in expected)
            {
                var check = new VisionClumpCheck{source=source.Key,sampled_focus=showcase.foliage.find_source_focus(source.Key)};
                var patch = VisionPlantLayout.find_patch(showcase.config.active_dark_layout,source.Key);
                check.configured_instances = patch.count;
                check.nominal_bed_ellipse_area_m2 = VisionPlantLayout.nominal_area(patch);
                check.eligible_bed_1m_grid_area_m2 = measure_vision_bed_area(patch);
                check.exact_ground = showcase.foliage.source_ground_probes.Find(item=>item.source == source.Key);
                for(int zoom=0;zoom<=1;zoom++)
                {
                    showcase.set_focus(check.sampled_focus,zoom);
                    for(int frame=0;frame<30;frame++) yield return null;
                    yield return new WaitForEndOfFrame();
                    var submitted = showcase.foliage.sample_source_visibility(showcase.review_camera,source.Key);
                    float delta = compare_same_frame_vision_source(source.Key,zoom == 0 ? "near" : "middle");
                    bool visible = delta > .000001f && submitted.submitted_matrix_submesh_count > 0;
                    if(zoom == 0)
                    {
                        check.near = submitted;
                        check.near_pixel_delta = delta;
                        check.near_pixel_pass = visible;
                        if(source.Key == "GrassMedium02E") sample_vision_grass_surface(check);
                    }
                    else
                    {
                        check.middle = submitted;
                        check.middle_pixel_delta = delta;
                        check.middle_pixel_pass = visible;
                    }
                }
                check.source_geometry_pass = check.near.source_triangles == source.Value && check.middle.source_triangles == source.Value;
                evidence.Add(check);
            }
            report.flat.vision_clumps = evidence.ToArray();
            report.flat.vision_clumps_pass = true;
            foreach(var check in evidence)
                report.flat.vision_clumps_pass &= check.source_geometry_pass && check.near_pixel_pass && check.middle_pixel_pass;
            showcase.set_view(0,1);
        }

        private void sample_vision_grass_surface(VisionClumpCheck check)
        {
            var settings = showcase.foliage.sample_source_surface_settings(check.source);
            if(settings.Length == 0) throw new InvalidOperationException("Actual Vision grass material unavailable for coverage probe.");
            try
            {
                // Independent same-frame tests; source geometry/count/cutoff and lighting remain identical.
                showcase.foliage.set_source_surface_settings(check.source,0,settings[0].y);
                check.grass_near_no_coverage_delta = compare_same_frame_vision_source(check.source,"near-no-alpha-coverage");
                showcase.foliage.set_source_surface_settings(check.source,settings[0].x,0);
                check.grass_near_zero_mip_bias_delta = compare_same_frame_vision_source(check.source,"near-zero-mip-bias");
            }
            finally
            {
                showcase.foliage.set_source_surface_settings(check.source,settings[0].x,settings[0].y);
            }
        }

        private float measure_vision_bed_area(DarkWildernessConfig.PlantPatch patch)
        {
            float area = 0;
            float size = showcase.config.map_size;
            for(float z=.5f;z<size;z++)
            {
                for(float x=.5f;x<size;x++)
                {
                    if(VisionPlantLayout.measure_patch(patch,x,z,size) <= .2f || !showcase.ground.can_place_greenery(x,z) || showcase.ground.concept.exposed_soil(x,z) >= .65f) continue;
                    area += 1;
                }
            }
            return area;
        }

        private IEnumerator run_vision_oak_checks()
        {
            const string SOURCE = "OakLauris71";
            var views = new List<ShowcaseFoliage.SourceVisibilityProbe>();
            var deltas = new List<float>();
            var prefab = showcase.config.active_dark_layout.tree_prefabs[0];
            bool valid = prefab && prefab.GetComponentsInChildren<LODGroup>(true).Length == 1;
            for(int zoom=0;zoom<3;zoom++)
            {
                showcase.set_view(0,zoom);
                showcase.set_focus(showcase.config.active_dark_layout.vision_tree_centers[0],zoom);
                for(int frame=0;frame<30;frame++) yield return null;
                var samples = new float[180];
                for(int frame=0;frame<samples.Length;frame++)
                {
                    yield return null;
                    samples[frame] = Time.unscaledDeltaTime*1000;
                }
                string label = "oak-"+new[]{"near","middle","far"}[zoom];
                timings.Add(build_timing(samples,zoom,true,label));
                yield return new WaitForEndOfFrame();
                var submitted = showcase.foliage.sample_source_visibility(showcase.review_camera,SOURCE);
                float delta = compare_same_frame_vision_source(SOURCE,label);
                views.Add(submitted);
                deltas.Add(delta);
                bool exclusive_lod = !(submitted.submitted_lod0_matrix_submeshes > 0 && submitted.submitted_lod1_matrix_submeshes > 0);
                if(showcase.config.active_dark_layout.vision_forest)
                    exclusive_lod = submitted.duplicate_lod_tree_origins == 0;
                valid &= submitted.source_triangles == 6145 && submitted.source_lod0_triangles == 5303 && submitted.source_lod1_triangles == 842 &&
                    submitted.source_lod0_parts == 36 && submitted.source_lod1_parts == 36 && submitted.source_unresolved_lod_parts == 0 &&
                    submitted.submitted_matrix_submesh_count > 0 && delta > .000001f && exclusive_lod;
            }
            report.flat.vision_oak_views = views.ToArray();
            report.flat.vision_oak_pixel_deltas = deltas.ToArray();
            report.flat.vision_oak_pass = valid;
            showcase.set_view(0,1);
        }

        private float compare_same_frame_vision_source(string source,string view)
        {
            string previous = showcase.foliage.hidden_source;
            Texture2D visible = null;
            Texture2D hidden = null;
            try
            {
                showcase.foliage.hidden_source = "";
                visible = capture_camera();
                showcase.foliage.hidden_source = source;
                hidden = capture_camera();
                string prefix = "vision-source-"+source+"-"+view;
                File.WriteAllBytes(Path.Combine(output_directory,prefix+"-on.png"),visible.EncodeToPNG());
                File.WriteAllBytes(Path.Combine(output_directory,prefix+"-off.png"),hidden.EncodeToPNG());
                return measure_pixel_delta(visible.GetPixels32(),hidden.GetPixels32());
            }
            finally
            {
                showcase.foliage.hidden_source = previous;
                if(visible) Destroy(visible);
                if(hidden) Destroy(hidden);
            }
        }
    }
}
