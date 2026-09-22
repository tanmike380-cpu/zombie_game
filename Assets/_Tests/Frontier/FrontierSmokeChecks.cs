using System;
using System.Collections;
using UnityEngine;
using UnityEngine.AI;
using ZombieGame.World;
using ZombieGame.Balance;

namespace ZombieGame.FrontierTests
{
    public sealed class FrontierSmokeChecks : MonoBehaviour
    {
        private bool environment_camera_fixture;
        private Vector3 environment_camera_focus;
        private void LateUpdate()
        {
            if(environment_camera_fixture)GetComponent<FrontierGame>().focus_camera(environment_camera_focus);
        }
        private IEnumerator Start()
        {
            if(Array.IndexOf(Environment.GetCommandLineArgs(),"-freeEnvironmentSmoke")>=0)
            {yield return check_free_environment();yield break;}
            if(Array.IndexOf(Environment.GetCommandLineArgs(),"-frontierSmoke")<0)yield break;
            yield return null;
            var game=GetComponent<FrontierGame>();
            try
            {
                require(game.current!=null,"simulation startup");
                require(game.current.living_soldiers==400&&game.current.reserve_soldiers==200&&game.current.zombie_count==5000,"population and recruitment reserve");
                check_distribution(game);
                check_compact_settlement(game);
                foreach(var point in game.current.positions)
                    require(NavMesh.SamplePosition(point,out var hit,.3f,NavMesh.AllAreas),"spawn on native navigation");
                foreach(var region in game.map.regions)
                    require(!NavMesh.SamplePosition(new Vector3(region.bounds.center.x,0,region.bounds.center.z),out var hit,.25f,NavMesh.AllAreas),"natural obstacle nonwalkable: "+region.kind);
                var path=new NavMeshPath();
                require(NavMesh.CalculatePath(new Vector3(-60,0,-60),new Vector3(5,0,-60),NavMesh.AllAreas,path)&&path.status==NavMeshPathStatus.PathComplete,"land route around river");
                bool north=false;foreach(var corner in path.corners)if(corner.z>76)north=true;
                require(north,"route uses northern land opening, not river crossing");
                var config=JsonUtility.FromJson<FrontierEconomyConfig>(Resources.Load<TextAsset>("FrontierEconomy").text);
                var fixture=new FrontierEconomy(config);
                fixture.gunpowder=0;fixture.powder_works=false;
                require(!fixture.try_supply(0),"empty powder blocks shot");
                fixture.powder_works=true;fixture.step(1);
                float before=fixture.gunpowder;
                require(fixture.try_supply(0)&&Mathf.Abs(fixture.gunpowder-(before-UnitBalance.human.ammunition_cost))<.001f,"supply consumes exactly authored cost");
                require(game.current.try_supply_shot==null&&game.supply.depots.Count==1,"world uses carried ammo, not global per-shot spending");
                check_headquarters(game,config);
                FrontierFeatureChecks.run(game);
                ZombieGame.CharacterTests.CharacterVisualChecks.run();
                require(UnitBalance.config.zombie_sight==6&&UnitBalance.config.noise_propagation_speed==15,"requested sight and sound speed");
                for(int i=0;i<game.current.total_count;i++)
                    require(Mathf.Abs(game.current.crowd.agents[i].radius-UnitBalance.navigation_radius(game.current.stats_for(i)))<.001f,"shared per-role avoidance radius");
                Debug.Log("[FrontierSmoke] PASS map256 population400+5000 all spawns valid, every river/forest/cliff/building centre blocked, north detour reachable, powder gating and production");
            }
            catch(Exception exception){Debug.LogError("[FrontierSmoke] FAIL "+exception);Application.Quit(1);yield break;}
            yield return new WaitForSeconds(3);
            foreach(var facility in game.construction.facilities)
                require(!NavMesh.SamplePosition(facility.recipe.position,out var building_hit,.2f,NavMesh.AllAreas),"constructed footprint carved from native navigation");
            require(game.current.geometry_errors==0,"no geometry intrusion");
            Debug.Log("[FrontierSmoke] COMPLETE geometry=0");Application.Quit(0);
        }
        private IEnumerator check_free_environment()
        {
            yield return null;
            var game=GetComponent<FrontierGame>();
            bool legacy=Array.IndexOf(Environment.GetCommandLineArgs(),"-legacyEnvironment")>=0;
            string variant=legacy?"legacy":"cc0";
            try
            {
                int textured=0,rock_groups=0;
                foreach(var renderer in game.GetComponentsInChildren<MeshRenderer>())
                {
                    foreach(var material in renderer.sharedMaterials)
                    {
                        require(material!=null&&material.shader.isSupported,"supported environment material");
                        if(material.HasProperty("_TextureMode")&&material.GetFloat("_TextureMode")>0)textured++;
                    }
                }
                foreach(var transform in game.GetComponentsInChildren<Transform>())
                    if(transform.name=="CC0 moss rocks")
                    {
                        rock_groups++;
                        require(transform.GetComponentsInChildren<Collider>().Length==0,"art imports must not add blockers");
                        require(transform.GetComponent<LODGroup>()!=null,"imported rocks have distance culling");
                    }
                require(legacy?textured==0:textured>=10,"ground and building scanned materials");
                require(legacy?rock_groups==0:rock_groups>0&&rock_groups<=12,"bounded imported FBX groups");
                require(game.current.living_soldiers==400&&game.current.zombie_count==5000,"art does not alter population");
                Debug.Log($"[FreeEnvironmentSmoke] variant={variant} textured={textured} rock_groups={rock_groups}");
            }
            catch(Exception exception){Debug.LogError("[FreeEnvironmentSmoke] FAIL "+exception);Application.Quit(1);yield break;}
            environment_camera_fixture=true;environment_camera_focus=game.map.base_center;
            Camera.main.orthographicSize=24;game.focus_camera(environment_camera_focus);
            yield return new WaitForSecondsRealtime(2);
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot("/tmp/zombie-environment-"+variant+".png");
            var samples=new System.Collections.Generic.List<float>();
            float deadline=Time.realtimeSinceStartup+8;
            while(Time.realtimeSinceStartup<deadline)
            {yield return null;samples.Add(Time.unscaledDeltaTime*1000);}
            samples.Sort();
            Debug.Log($"[FreeEnvironmentSmoke] PASS variant={variant} resolution={Screen.width}x{Screen.height} samples={samples.Count} frame_ms_median={samples[samples.Count/2]:F2} p95={samples[Mathf.Min(samples.Count-1,Mathf.FloorToInt(samples.Count*.95f))]:F2}; startup idle snapshot, not siege benchmark");
            // Art-only inspection after performance sampling; no fog override leaks into playable mode.
            game.enabled=false;game.controls.enabled=false;
            foreach(var site in game.map.resource_sites)if(site.kind=="Stone")
            {
                environment_camera_focus=site.position;Camera.main.orthographicSize=7;
                yield return null;yield return new WaitForEndOfFrame();
                ScreenCapture.CaptureScreenshot("/tmp/zombie-environment-rocks-"+variant+".png");
                break;
            }
            yield return null;yield return new WaitForEndOfFrame();
            environment_camera_fixture=false;game.enabled=true;game.controls.enabled=true;
            Application.Quit(0);
        }
        private static void require(bool success,string message)
        {if(!success)throw new InvalidOperationException(message);}

        private static void check_compact_settlement(FrontierGame game)
        {
            Bounds footprint=new Bounds(game.map.headquarters_position,Vector3.zero);
            int buildings=0;
            foreach(var region in game.map.regions)
            {
                if(region.kind!=LandscapeKind.Building)continue;
                footprint.Encapsulate(region.bounds);buildings++;
            }
            require(buildings==9&&footprint.size.x<=34&&footprint.size.z<=30,"compact nine-building settlement footprint");
            foreach(var building in game.current.buildings)
            {
                Vector3 toward=new Vector3(-94,0,-78)-building.bounds.center;toward.y=0;
                Vector3 entrance=building.bounds.ClosestPoint(building.bounds.center+toward.normalized*20)+toward.normalized*1.6f;entrance.y=0;
                require(NavMesh.SamplePosition(entrance,out var hit,2,NavMesh.AllAreas),"compact building has accessible perimeter: "+building.label);
                var path=new NavMeshPath();
                require(NavMesh.CalculatePath(new Vector3(-94,0,-78),hit.position,NavMesh.AllAreas,path)&&path.status==NavMeshPathStatus.PathComplete,"muster can route to compact building: "+building.label);
            }
            Debug.Log($"[CompactSettlementSmoke] PASS buildings={buildings} footprint={footprint.size.x:F1}x{footprint.size.z:F1}; all perimeters connected to muster");
        }

        private static void check_distribution(FrontierGame game)
        {
            var config=ZombieDistribution.load();var counts=new int[config.bands.Length];
            var roles=new int[config.bands.Length,UnitBalance.zombie_ids.Length];var occupied=new System.Collections.Generic.HashSet<Vector3>();
            for(int i=FrontierMap.HUMAN_CAPACITY;i<game.map.spawns.Length;i++)
            {
                Vector3 point=game.map.spawns[i];
                int band=ZombieDistribution.find_band(config,Vector3.Distance(point,game.map.headquarters_position));
                require(band>=0&&occupied.Add(point),"valid unique distance-band spawn");counts[band]++;
                string id=game.map.unit_ids[i];roles[band,Array.IndexOf(UnitBalance.zombie_ids,id)]++;
                require(game.current.stats_for(i)==UnitBalance.get(id)&&game.current.crowd.agents[i].speed==UnitBalance.get(id).move_speed,"all distance-band roles use shared stats");
                require(!(point.x<-40&&point.z<-48),"settlement remains safe");
            }
            float cumulative=0;int allocated=0;
            for(int i=0;i<counts.Length;i++)
            {
                var band=config.bands[i];cumulative+=band.population_percent;
                int end=i==counts.Length-1?FrontierMap.ZOMBIES:Mathf.RoundToInt(FrontierMap.ZOMBIES*cumulative/100);
                require(counts[i]==end-allocated,"distance-band population quota");allocated=end;
                int[] expected=ZombieDistribution.role_counts(band,counts[i]);
                for(int role=0;role<band.roles.Length;role++)require(roles[i,Array.IndexOf(UnitBalance.zombie_ids,band.roles[role].id)]==expected[role],"exact role quota: "+band.roles[role].id);
            }
            for(int role=0;role<UnitBalance.zombie_ids.Length;role++)
            {
                int count=0;for(int band=0;band<config.bands.Length;band++)count+=roles[band,role];
                require(count>0,"all eight designed types present: "+UnitBalance.zombie_ids[role]);
            }
            config.bands[0].population_percent+=1;
            bool rejected=false;try{ZombieDistribution.validate(config);}catch(InvalidOperationException){rejected=true;}
            require(rejected,"invalid percentages rejected instead of silently changing population");
            Debug.Log("[DistributionSmoke] PASS exact quotas/role shares, unique cells, safe settlement, shared stats, invalid config rejected");
        }

        private static void check_headquarters(FrontierGame game,FrontierEconomyConfig economy_config)
        {
            var fixture_economy=new FrontierEconomy(economy_config);
            var config=JsonUtility.FromJson<HeadquartersConfig>(Resources.Load<TextAsset>("HeadquartersProduction").text);
            var fixture=new HeadquartersProduction(fixture_economy,config);
            float food=fixture_economy.food,iron=fixture_economy.iron;
            require(fixture.enqueue(0,200),"enqueue soldier");
            require(fixture_economy.food==food-config.recipes[0].food,"deduct training cost once");
            fixture.cancel_last();require(fixture_economy.food==food&&fixture_economy.iron==iron,"cancel refunds costs");
            fixture_economy.food=0;require(!fixture.enqueue(0,200)&&fixture.queue.Count==0,"insufficient resources rejects production");
            fixture_economy.food=food;require(!fixture.enqueue(0,0),"capacity rejects training");
            require(fixture.enqueue(0,200),"queue production again");
            fixture.step(config.recipes[0].seconds,_=>false);require(fixture.queue.Count==1&&fixture.progress==1,"blocked exit retains paid queue");
            fixture.step(0,_=>true);require(fixture.queue.Count==0,"retry blocked completion");
            require(!fixture.enqueue(1,200),"unplaced building rejected");
            require(game.finish_production(config.recipes[0]),"recruit onto clear native navigation");
            require(game.current.living_soldiers==401&&game.current.reserve_soldiers==199,"new soldier becomes playable");
            int index=FrontierMap.SOLDIERS;
            require(game.current.health[index]==UnitBalance.human.health&&game.current.crowd.agents[index].speed==UnitBalance.human.move_speed,"recruit reads shared combat balance");
            game.controls.select_all();require(game.controls.selected_count()==401,"F2 includes recruit");
            Debug.Log("[HeadquartersSmoke] PASS costs/refunds/resource rejection/capacity/queue blockage/unplaced rejection/native recruit/shared stats/F2");
            // Smoke-only mutations are confined to this disposable standalone test process.
        }
    }
}
