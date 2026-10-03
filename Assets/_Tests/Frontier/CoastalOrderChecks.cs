using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using ZombieGame.Combat;
using ZombieGame.Movement;

namespace ZombieGame.FrontierTests
{
    /// <summary>Disposable sealed-wall, breach and corner-command fixtures; no balance overrides.</summary>
    public static class CoastalOrderChecks
    {
        public static IEnumerator run()
        {
            var points=new[]{new Vector3(-5,0,-18),new Vector3(5,0,-18)};
            using(var battle=new BattleSimulation(points,2,new bool[2],Array.Empty<Bounds>(),
                unit_ids:new[]{"greek_fire","heavy_crossbowman"}))
            {
                var bounds=new Bounds(new Vector3(0,1,0),new Vector3(256,2,4));
                var barrier=battle.crowd.add_building(bounds);
                yield return new WaitForSeconds(.5f);
                for(int i=0;i<2;i++)require(battle.issue_order(i,SoldierOrder.AttackMove,new Vector3(points[i].x,0,18)),"accept sealed A command");
                for(float end=Time.time+12;Time.time<end;){battle.step(Time.time,Time.deltaTime);yield return null;}
                for(int i=0;i<2;i++)
                {
                    require(battle.positions[i].z> -8&&battle.positions[i].z< -2,"approach reachable wall boundary");
                    require(battle.orders[i]==SoldierOrder.AttackMove,"retain original A objective");
                }
                battle.crowd.remove_building(bounds,barrier);
                for(float end=Time.time+18;Time.time<end;){battle.step(Time.time,Time.deltaTime);yield return null;}
                for(int i=0;i<2;i++)require(battle.positions[i].z>14,"resume after wall breach without another click");
            }
            yield return null;
            yield return check_corners();
            yield return check_gate();
            Debug.Log("[CoastalOrders] PASS partial A routes, breach recovery, four corner formations");
        }
        private static IEnumerator check_gate()
        {
            var gate=new Bounds(new Vector3(0,1,0),new Vector3(10,2,9));
            var walls=new[]{new Bounds(new Vector3(-67,1,0),new Vector3(124,2,9)),
                new Bounds(new Vector3(67,1,0),new Vector3(124,2,9))};
            using(var battle=new BattleSimulation(new[]{new Vector3(0,0,-12),new Vector3(2,0,14)},1,new bool[2],walls,
                unit_ids:new[]{"heavy_crossbowman","walker"},friendly_gates:new[]{gate}))
            {
                var carving=battle.crowd.add_friendly_gate(gate);
                yield return new WaitForSeconds(.5f);
                var path=new NavMeshPath();var enemy=battle.crowd.agents[1];
                enemy.enabled=true;
                require(enemy.CalculatePath(new Vector3(2,0,-14),path)&&path.status==NavMeshPathStatus.PathPartial,"enemy cannot cross living gate");
                enemy.isStopped=true;enemy.enabled=false; // Disposable noncombat gate-permission fixture.
                float fixture_enemy_health=battle.health[1];battle.health[1]=0; // Isolate faction routing from fighting; restored below and simulation disposed.
                require(battle.issue_order(0,SoldierOrder.Move,new Vector3(0,0,12)),"friendly gate route issued");
                for(float end=Time.time+12;Time.time<end;){battle.step(Time.time,Time.deltaTime);yield return null;}
                require(battle.positions[0].z>9,"friendly physically crosses northbound without warping");
                require(battle.issue_order(0,SoldierOrder.Move,new Vector3(0,0,-12)),"southbound gate route issued");
                for(float end=Time.time+12;Time.time<end;){battle.step(Time.time,Time.deltaTime);yield return null;}
                require(battle.positions[0].z< -9,"friendly physically crosses southbound");
                battle.crowd.remove_building(gate,carving);yield return new WaitForSeconds(.5f);
                battle.health[1]=fixture_enemy_health;
                enemy.enabled=true;
                require(enemy.CalculatePath(new Vector3(2,0,-14),path)&&path.status==NavMeshPathStatus.PathComplete,"destroyed gate opens to enemies");
            }
            Debug.Log("[CoastalGate] PASS friendly two-way passage, enemy filter, destruction opens route");
        }
        private static IEnumerator check_corners()
        {
            var points=new Vector3[40];var ids=new string[40];
            for(int i=0;i<40;i++){points[i]=new Vector3(i%8*2-7,0,i/8*2-4);ids[i]=i<4?"greek_fire":"heavy_crossbowman";}
            using(var battle=new BattleSimulation(points,40,new bool[40],Array.Empty<Bounds>(),unit_ids:ids))
            {
                foreach(var goal in new[]{new Vector3(-127,0,-127),new Vector3(-127,0,127),new Vector3(127,0,127),new Vector3(127,0,-127)})
                {
                    var slots=new List<Vector3>();
                    require(new FormationDestinations().build(goal,new List<Vector3>(battle.positions),slots),"corner formation fits map");
                    var before=(Vector3[])battle.positions.Clone();
                    for(int i=0;i<40;i++)require(battle.issue_order(i,SoldierOrder.AttackMove,slots[i]),"every selected unit receives corner A");
                    for(float end=Time.time+3;Time.time<end;){battle.step(Time.time,Time.deltaTime);yield return null;}
                    for(int i=0;i<40;i++)require(Vector3.Distance(before[i],battle.positions[i])>.3f,"no silently idle corner unit: "+i);
                }
            }
        }
        private static void require(bool condition,string message)
        {if(!condition){Debug.LogError("[CoastalOrders] FAIL "+message);Application.Quit(1);throw new InvalidOperationException(message);}}
    }
}
