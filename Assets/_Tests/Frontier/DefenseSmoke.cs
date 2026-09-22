using System;
using System.Collections;
using UnityEngine;
using ZombieGame.World;

namespace ZombieGame.FrontierTests
{
    /// <summary>Full-map disposable run. Advances only the scenario clock, never unit speed or health.</summary>
    public sealed class DefenseSmoke:MonoBehaviour
    {
        private ZombieGame.Combat.BattleSimulation objective_fixture;
        private bool objective_only;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void install_fixture()
        {
            bool objective=Array.IndexOf(Environment.GetCommandLineArgs(),"-siegeObjectiveSmoke")>=0;
            if(!objective&&Array.IndexOf(Environment.GetCommandLineArgs(),"-defenseSmoke")<0)return;
            if(objective)FindFirstObjectByType<FrontierGame>().enabled=false;
            new GameObject("Full-map defense regression").AddComponent<DefenseSmoke>().objective_only=objective;
            Application.logMessageReceived+=fail_on_error;
        }
        private static void fail_on_error(string message,string trace,LogType type)
        {if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)Application.Quit(1);}
        private IEnumerator Start()
        {
            Application.runInBackground=true;
            if(objective_only){yield return check_siege_objective();yield break;}
            var game=FindFirstObjectByType<FrontierGame>();
            while(game.current==null)yield return null;
            game.focus_camera(new Vector3(-99,0,-92));
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot("/tmp/zombie-defense-ui.png");
            yield return null;
            game.siege.step(game.siege.remaining,game.current,game.construction,game.map.base_center);
            require(game.siege.waves==1&&game.current.siege_units==game.current.zombie_count-game.current.dead_zombies,"all surviving map zombies ordered once");
            float finish=Time.time+12;int peak_moving=0;
            while(Time.time<finish)
            {peak_moving=Mathf.Max(peak_moving,game.current.moving_now);yield return null;}
            Debug.Log($"[DefenseSmoke] navigation moving_peak={peak_moving} pending={game.current.pending} path_failures={game.current.path_failures} active={game.current.active_now}");
            require(peak_moving>1000,"native navigation moves the mass assault");
            require(game.current.geometry_errors==0,"no geometry intrusion");
            require(game.can_control,"player controls retained");
            Debug.Log($"[DefenseSmoke] PASS ordered={game.current.siege_units} moving_peak={peak_moving} geometry=0 human_controls=true");
            Application.Quit(0);
        }
        private void Update()
        {objective_fixture?.step(Time.time,Time.deltaTime);}

        private IEnumerator check_siege_objective()
        {
            var bounds=new Bounds(new Vector3(0,1.5f,0),new Vector3(6,3,6));
            var decoy_bounds=new Bounds(new Vector3(19,1.5f,14),new Vector3(4,3,4));
            objective_fixture=new ZombieGame.Combat.BattleSimulation(new[]{new Vector3(-65,0,-65),new Vector3(22,0,0)},1,new bool[2],new[]{bounds,decoy_bounds});
            var headquarters=objective_fixture.add_building_target(bounds,"HQ OBJECTIVE FIXTURE",true);
            var decoy=objective_fixture.add_building_target(decoy_bounds,"NEARER DECOY FIXTURE");
            objective_fixture.try_supply_shot=_=>false; // Disposable ceasefire, no balance override.
            objective_fixture.order_siege(new Vector3(22,0,0),1); // Old rally point is deliberately the zombie's current position.
            float deadline=Time.time+12;
            while(headquarters.health==headquarters.max_health&&Time.time<deadline)yield return null;
            require(headquarters.health<headquarters.max_health,"silent siege leaves rally point and attacks HQ without gunfire");
            require(decoy.health==decoy.max_health,"closer remote building does not replace HQ objective");
            float before=headquarters.health;
            objective_fixture.emit_gun_noise(new Vector3(3,0,14),Time.time);
            yield return new WaitForSeconds(2.5f);
            require(headquarters.health<before,"new sound cannot pull committed zombie away from HQ");
            var agent=objective_fixture.crowd.agents[1];
            require(agent.Warp(new Vector3(12,0,0)),"fixture relocate after path reset");agent.ResetPath();
            before=headquarters.health;deadline=Time.time+8;
            while(headquarters.health==before&&Time.time<deadline)yield return null;
            require(headquarters.health<before,"lost path retries and resumes silent HQ attack");
            require(objective_fixture.shots==0&&objective_fixture.geometry_errors==0,"no human gunfire or geometry intrusion");
            Debug.Log("[SiegeObjectiveSmoke] PASS silent HQ pursuit/ignore old rally/ignore remote decoy/no sound diversion/path recovery; shared stats unchanged");
            objective_fixture.try_supply_shot=null;objective_fixture.Dispose();objective_fixture=null;
            yield return check_attack_move();
            yield return check_ordinary_perception();
            Application.Quit(0);
        }
        private IEnumerator check_attack_move()
        {
            var bounds=new Bounds(new Vector3(0,1.5f,0),new Vector3(6,3,6));
            var decoy_bounds=new Bounds(new Vector3(20,1.5f,15),new Vector3(4,3,4));
            objective_fixture=new ZombieGame.Combat.BattleSimulation(
                new[]{new Vector3(21,0,1),new Vector3(24,0,-3),new Vector3(24,0,4),new Vector3(20,0,0)},
                3,new bool[4],new[]{bounds,decoy_bounds},unit_ids:new[]{"firearm_infantry","firearm_infantry","firearm_infantry","brute"});
            var headquarters=objective_fixture.add_building_target(bounds,"ATTACK MOVE HQ FIXTURE",true);
            var decoy=objective_fixture.add_building_target(decoy_bounds,"POST HQ DECOY FIXTURE");
            objective_fixture.try_supply_shot=_=>false;
            objective_fixture.order_siege(Vector3.zero,1);
            yield return new WaitForSeconds(.2f);
            require((objective_fixture.zombie_navigation_goal(3)-objective_fixture.positions[0]).sqrMagnitude<.1f,"encountered nearby human interrupts siege");
            // Disposable death fixture: the blocking soldier dies; other soldiers leave sight.
            objective_fixture.health[0]=0;objective_fixture.crowd.agents[0].enabled=false;
            require(objective_fixture.crowd.agents[1].Warp(new Vector3(-65,0,-60)),"first bystander disengage fixture");
            require(objective_fixture.crowd.agents[2].Warp(new Vector3(-65,0,-65)),"second bystander disengage fixture");
            float deadline=Time.time+15;
            while(headquarters.health==headquarters.max_health&&Time.time<deadline)yield return null;
            require(headquarters.health<headquarters.max_health,"dead blocking soldier resumes silent headquarters attack");
            objective_fixture.damage_building(headquarters,headquarters.health,Time.time);
            yield return new WaitForSeconds(1);
            require(decoy.health==decoy.max_health&&objective_fixture.crowd.agents[3].isStopped,"HQ loss completes objective, no remote building sweep");
            Debug.Log("[SiegeAttackMoveSmoke] PASS encountered human/dead target resumes HQ/HQ completion");
            objective_fixture.try_supply_shot=null;objective_fixture.Dispose();objective_fixture=null;
        }
        private IEnumerator check_ordinary_perception()
        {
            objective_fixture=new ZombieGame.Combat.BattleSimulation(new[]{new Vector3(22,0,3),new Vector3(20,0,0)},1,new bool[2],Array.Empty<Bounds>());
            objective_fixture.try_supply_shot=_=>false;
            yield return new WaitForSeconds(.1f);
            Vector3 sound_origin=new Vector3(30,0,0);
            objective_fixture.emit_gun_noise(sound_origin,Time.time);
            yield return new WaitForSeconds(1);
            require((objective_fixture.zombie_navigation_goal(1)-objective_fixture.positions[0]).sqrMagnitude<.1f,"ordinary sight overrides simultaneous audible sound");
            require(objective_fixture.crowd.agents[0].Warp(new Vector3(-65,0,-65)),"ordinary visual loss fixture");
            yield return new WaitForSeconds(.3f);
            require((objective_fixture.zombie_navigation_goal(1)-sound_origin).sqrMagnitude<.1f,"visual loss restores latest unfinished sound, not old chase point");
            yield return new WaitForSeconds(4);
            require((objective_fixture.positions[1]-sound_origin).sqrMagnitude<1&&objective_fixture.crowd.agents[1].isStopped,"ordinary sound investigation stops at source without new stimulus");
            Debug.Log("[OrdinaryPerceptionSmoke] PASS sight over sound/visual loss restores sound/arrival settles; no siege objective");
            objective_fixture.try_supply_shot=null;objective_fixture.Dispose();objective_fixture=null;
        }
        private static void require(bool condition,string message){if(!condition)throw new InvalidOperationException("Defense regression: "+message);}
        private void OnDestroy(){objective_fixture?.Dispose();Application.logMessageReceived-=fail_on_error;}
    }
}
