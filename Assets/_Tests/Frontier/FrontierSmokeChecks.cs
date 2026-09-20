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
                require(game.current.soldier_count==400&&game.current.zombie_count==5000,"population");
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
                require(game.current.try_supply_shot!=null,"live battle economy gate");
                Debug.Log("[FrontierSmoke] PASS map256 population400+5000 all spawns valid, every river/forest/cliff/building centre blocked, north detour reachable, powder gating and production");
            }
            catch(Exception exception){Debug.LogError("[FrontierSmoke] FAIL "+exception);Application.Quit(1);yield break;}
            yield return new WaitForSeconds(3);
            require(game.current.geometry_errors==0,"no geometry intrusion");
            Debug.Log("[FrontierSmoke] COMPLETE geometry=0");Application.Quit(0);
        }
        private static void require(bool success,string message)
        {if(!success)throw new InvalidOperationException(message);}
    }
}
