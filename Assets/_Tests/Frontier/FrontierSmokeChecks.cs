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
        private IEnumerator Start()
        {
            if(Array.IndexOf(Environment.GetCommandLineArgs(),"-frontierSmoke")<0)yield break;
            yield return null;
            var game=GetComponent<FrontierGame>();
            try
            {
                require(game.current!=null,"simulation startup");
                require(game.current.living_soldiers==400&&game.current.reserve_soldiers==200&&game.current.zombie_count==5000,"population and recruitment reserve");
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
                    require(Mathf.Abs(game.current.crowd.agents[i].radius-UnitBalance.config.unit_navigation_radius)<.001f,"shared avoidance radius");
                Debug.Log("[FrontierSmoke] PASS map256 population400+5000 all spawns valid, every river/forest/cliff/building centre blocked, north detour reachable, powder gating and production");
            }
            catch(Exception exception){Debug.LogError("[FrontierSmoke] FAIL "+exception);Application.Quit(1);yield break;}
            yield return new WaitForSeconds(3);
            foreach(var facility in game.construction.facilities)
                require(!NavMesh.SamplePosition(facility.recipe.position,out var building_hit,.2f,NavMesh.AllAreas),"constructed footprint carved from native navigation");
            require(game.current.geometry_errors==0,"no geometry intrusion");
            Debug.Log("[FrontierSmoke] COMPLETE geometry=0");Application.Quit(0);
        }
        private static void require(bool success,string message)
        {if(!success)throw new InvalidOperationException(message);}

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
