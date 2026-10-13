using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ZombieGame.Tests.EnvironmentShowcase
{
    public sealed partial class ShowcaseChecks
    {
        [Serializable] private sealed class DarkSourceReport
        {
            public string scope="Light source/fixture/LOD diagnostic only; no full art or battle acceptance";
            public string grass_measurement="32 submitted source matrices each; actual transformed mesh bounds vs root Terrain anchor, conservative terrain bbox-corner span; not alpha-visible leaf geometry.";
            public bool passed,road_zero,no_river,forest_source_available,visual_accepted;
            public float maximum_road_weight;
            public int buildings,iron,gold,natural_stone_candidates;
            public FixtureReport[] fixtures;
            public string[] boulder_lod_states;
            public ShowcaseFoliage.GrassProbe[] grass_probes;
        }
        private IEnumerator run_dark_checks()
        {
            showcase.show_hud=false;
            report.biome="dark-v1";
            report.biome_label="Cold wilderness source-prop technical preview; visual acceptance pending";
            report.unavailable_art=showcase.missing_art;
            report.implemented=new[]{"Independent cold-neutral presentation", "Seven source buildings and six source iron/gold fixtures", "Natural disconnected soil/rock exposure; Road zero", "Separate sea and pond with irregular low-amplitude water variation"};
            for(int view=0;view<4;view++)
                for(int zoom=0;zoom<2;zoom++)
            {
                showcase.set_view(view,zoom);
                for(int frame=0;frame<30;frame++) yield return null;
                yield return new WaitForEndOfFrame();
                save_camera_image("dark-"+view+"-"+(zoom==0?"near":"middle")+".png");
            }
            var checks=sample_dark_sources();
            while(checks.MoveNext()) yield return checks.Current;
            report.passed=report.dark_source.passed;
            report.source_complete=false;
            report.error="Appropriate dense forest source unavailable. Technical source snapshot is not full art acceptance; old-layout regressions reported separately.";
        }

        private IEnumerator sample_dark_sources()
        {
            var evidence=new DarkSourceReport{buildings=showcase.landmarks.buildings.Count,no_river=!showcase.ground.routes.enabled,
                forest_source_available=showcase.config.dark_layout.tree_prefabs.Length>0,visual_accepted=false,grass_probes=showcase.foliage.grass_probes.ToArray()};
            var fixtures=new List<GameObject>(showcase.landmarks.buildings);
            fixtures.AddRange(showcase.landmarks.ores);
            var results=new List<FixtureReport>();
            var lod_states=new List<string>();
            evidence.passed=fixtures.Count==16;
            for(int index=0;index<fixtures.Count;index++)
            {
                var source=showcase.landmarks.placed_sources[index];
                var placement=showcase.landmarks.placements[index];
                var instance=fixtures[index];
                showcase.set_focus(placement.center,0);
                for(int frame=0;frame<8;frame++) yield return null;
                yield return new WaitForEndOfFrame();
                var fixture=sample_dark_fixture(instance,source,placement,index);
                results.Add(fixture);
                evidence.passed &= fixture.source_transform_pass&&fixture.ground_pass&&fixture.material_channels_pass&&fixture.pixel_pass;
                if(source.id=="IronOreRock") evidence.iron++;
                if(source.id=="GoldOreRock") evidence.gold++;
                if(source.id=="BoulderNative")
                {
                    evidence.natural_stone_candidates++;
                    evidence.passed &= check_boulder_lod_state(instance,lod_states);
                }
            }
            evidence.fixtures=results.ToArray();
            evidence.boulder_lod_states=lod_states.ToArray();
            var weights=new float[6];
            for(int z=0;z<=64;z++) for(int x=0;x<=64;x++)
            {
                showcase.ground.sample_weights(x/64f,z/64f,weights);
                evidence.maximum_road_weight=Mathf.Max(evidence.maximum_road_weight,weights[5]);
            }
            evidence.road_zero=evidence.maximum_road_weight==0;
            evidence.passed &= evidence.no_river&&evidence.road_zero;
            report.dark_source=evidence;
        }

        private FixtureReport sample_dark_fixture(GameObject instance,EnvironmentShowcaseConfig.SourceAsset source,DarkWildernessConfig.Placement placement,int index)
        {
            float size=showcase.config.map_size;
            float ground=showcase.ground.sample_ground(placement.center.x*size,placement.center.y*size).y;
            float bottom=instance.transform.position.y+source.native_min_y*placement.uniform_scale;
            var fixture=new FixtureReport{id=instance.name,source=source.source_record,authored_native_dimensions_m=source.native_dimensions,
                world_position=instance.transform.position,whole_uniform_scale=placement.uniform_scale,ground_height_error_m=Mathf.Abs(bottom-ground),whole_root_translation=instance.transform.position-source.prefab.transform.position};
            fixture.source_transform_pass=compare_source_transforms(instance,source.prefab)&&Vector3.Distance(instance.transform.localScale,source.prefab.transform.localScale*placement.uniform_scale)<.00001f;
            fixture.footprint_ground_span_m=measure_footprint_ground(instance,source,placement.uniform_scale);
            fixture.ground_pass=fixture.ground_height_error_m<.003f&&fixture.footprint_ground_span_m<(placement.ore?.5f:.08f)&&instance.GetComponentsInChildren<Collider>(true).Length==0;
            fixture.material_channels_pass=true;
            foreach(var binding in source.material_bindings)
                fixture.material_channels_pass &= binding.material&&binding.import_channels_pass&&(!binding.base_color||binding.material.GetTexture("_MainTex")==binding.base_color);
            fixture.pixel_delta=compare_same_frame_object(instance,"dark-source-"+index);
            fixture.pixel_pass=fixture.pixel_delta>(placement.ore?.00001f:.0001f);
            return fixture;
        }

        private static bool check_boulder_lod_state(GameObject instance,List<string> states)
        {
            var groups=instance.GetComponentsInChildren<LODGroup>(true);
            bool passed=groups.Length==1;
            foreach(var renderer in instance.GetComponentsInChildren<MeshRenderer>(true))
                if(renderer.name=="boulder_01_LOD0"||renderer.name=="boulder_01_LOD1") passed &= !renderer.enabled;
            if(groups.Length==1)
            {
                var lods=groups[0].GetLODs();
                passed &= lods.Length==2&&lods[0].renderers.Length==1&&lods[1].renderers.Length==1&&lods[0].renderers[0]!=lods[1].renderers[0]&&
                    lods[0].renderers[0].name=="boulder_01_LOD2"&&lods[1].renderers[0].name=="boulder_01_LOD3";
            }
            states.Add(instance.name+": "+(passed?"PASS":"FAIL")+"; unique native-select LODGroup, LOD0/1 disabled, disjoint LOD2/3. Unity selects one tier; not instanced or manually double-submitted.");
            return passed;
        }
    }
}
