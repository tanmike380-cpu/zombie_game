using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using ZombieGame.Balance;
using ZombieGame.Combat;
using ZombieGame.Movement;
using ZombieGame.Presentation;

namespace ZombieGame.CharacterTests
{
    /// <summary>Non-mutating spacing diagnostics and a disposable navigation/effect regression fixture.</summary>
    public static class CrowdSpacingChecks
    {
        public static float minimum_ratio(BattleSimulation battle)
        {
            float cell_size = 0;
            for (int i = 0; i < battle.total_count; i++)
                cell_size = Mathf.Max(cell_size, UnitBalance.navigation_radius(battle.stats_for(i)) * 2);
            var cells = new Dictionary<Vector2Int, List<int>>();
            float minimum = 1;
            for (int i = 0; i < battle.total_count; i++)
            {
                if (battle.health[i] <= 0) continue;
                Vector3 point = battle.positions[i];
                var key = new Vector2Int(Mathf.FloorToInt(point.x / cell_size), Mathf.FloorToInt(point.z / cell_size));
                for (int x = -1; x <= 1; x++)
                    for (int z = -1; z <= 1; z++)
                        if (cells.TryGetValue(key + new Vector2Int(x,z), out var neighbors))
                            foreach (int neighbor in neighbors)
                            {
                                float ratio=Vector3.Distance(point,battle.positions[neighbor]) / battle.contact_distance(i,neighbor);
                                if(ratio<minimum && ratio<.5f)
                                    Debug.Log($"[CrowdSpacingPair] {i}/{neighbor} ratio={ratio:F3} type={battle.stats_for(i).id}/{battle.stats_for(neighbor).id} stopped={battle.crowd.agents[i].isStopped}/{battle.crowd.agents[neighbor].isStopped} point={point}");
                                minimum = Mathf.Min(minimum, ratio);
                            }
                if (!cells.TryGetValue(key, out var bucket)) cells[key] = bucket = new List<int>();
                bucket.Add(i);
            }
            return minimum;
        }

        public static IEnumerator run()
        {
            yield return check_slime();
            yield return check_chase_recovery();
            // No health/speed/warp overrides: the fixture owns and disposes its complete simulation.
            float spacing = UnitBalance.config.formation_spacing;
            var positions = new Vector3[128];
            for (int i = 0; i < positions.Length; i++)
                positions[i] = new Vector3((i % 8 - 3.5f)*spacing, 0, (i<64 ? -20 : 20) + (i%64/8-3.5f)*spacing);
            var obstacles = new[] { new Bounds(new Vector3(0,1,44), new Vector3(18,2,3)) };
            using (var battle = new BattleSimulation(positions,128,new bool[128],obstacles))
            {
                yield return null;
                foreach (var goal in new[] { Vector3.zero, new Vector3(0,0,62) })
                {
                    var slots = new List<Vector3>();
                    require(new FormationDestinations().build(goal,new List<Vector3>(battle.positions),slots), "formation assignment");
                    for (int i = 0; i < 128; i++) require(battle.issue_order(i,SoldierOrder.AttackMove,slots[i]), "attack move route");
                    bool detour = goal.z < 1;
                    float until = Time.time + 60;
                    while (Time.time < until)
                    {
                        battle.step(Time.time,Time.deltaTime);
                        foreach (var agent in battle.crowd.agents) if (agent.hasPath && agent.path.corners.Length > 2) detour = true;
                        yield return null;
                    }
                    int arrived = 0, compact = 0, moving = 0, accepted = 0, crossed_wall = 0;
                    float formation_radius = 0;
                    foreach(var slot in slots) formation_radius = Mathf.Max(formation_radius,Vector3.Distance(slot,goal));
                    for (int i = 0; i < 128; i++)
                    {
                        if (Vector3.Distance(battle.positions[i],slots[i]) < spacing*2) arrived++;
                        if (Vector3.Distance(battle.positions[i],goal) < formation_radius+spacing*2) compact++;
                        // Match the existing CrowdedArrival six-slot occupied-inner-goal contract, not an arbitrary percentage.
                        if (Vector3.Distance(battle.positions[i],slots[i]) <= spacing*6+.05f) accepted++;
                        if (goal.z<1 || battle.positions[i].z>wall_far_edge(obstacles[0])) crossed_wall++;
                        if (!battle.crowd.agents[i].isStopped) moving++;
                    }
                    float ratio = minimum_ratio(battle);
                    Debug.Log($"[CrowdSpacingChecks] goal={goal} near_assigned_slot={arrived}/128 compact={compact}/128 accepted={accepted}/128 crossed_wall={crossed_wall}/128 moving={moving} separation_ratio={ratio:F3} detour={detour}");
                    require(accepted==128 && crossed_wall==128 && detour && battle.geometry_errors == 0, "regroup / obstacle detour");
                    require(ratio >= .93f, "native collision footprint collapsed");
                }
            }
            yield return check_building_contact();
            Debug.Log("[CrowdSpacingChecks] PASS regroup, obstacle routing, separation and slime lifetime");
            Application.Quit(0);
        }

        public static IEnumerator run_chase()
        {
            yield return check_slime();
            yield return check_chase_recovery();
            Debug.Log("[ChaseRecoveryChecks] PASS");
            Application.Quit(0);
        }

        private static float wall_far_edge(Bounds obstacle) => obstacle.max.z + ZombieGame.Balance.UnitBalance.config.unit_navigation_radius;

        private static IEnumerator check_chase_recovery()
        {
            var positions=new Vector3[68]; var ids=new string[68]; var explosive=new bool[68];
            positions[0]=Vector3.zero; positions[1]=new Vector3(16,0,0);
            ids[0]=ids[1]="firearm_infantry";
            positions[2]=new Vector3(-.65f,0,.95f); positions[3]=new Vector3(.65f,0,.95f);
            for(int i=4;i<68;i++) positions[i]=new Vector3((i%8-3.5f)*1.3f,0,30+(i-4)/8*1.3f);
            for(int i=2;i<68;i++) {ids[i]="exploder";explosive[i]=true;}
            using(var battle=new BattleSimulation(positions,2,explosive,Array.Empty<Bounds>(),global_assault:true,unit_ids:ids))
            {
                // Named no-fire fixture: preserve real HP, movement and explosion damage; cleared before disposal.
                battle.try_supply_shot=_=>false;
                try
                {
                    battle.forced_target=_=>battle.health[0]>0?0:battle.health[1]>0?1:-1;
                    for(int i=2;i<68;i++) require(battle.prepare_assault_path(i),"initial chase path");
                    Vector3 direction=(positions[0]-positions[67]).normalized;
                    require(Vector3.Dot(battle.crowd.agents[67].destination-positions[0],direction)>
                        UnitBalance.config.zombie_attack_move_follow_through-.1f,"chase endpoint lies beyond live target");
                    battle.release_assault();
                    bool attack_visible=false;
                    float until=Time.time+2;
                    while(Time.time<until)
                    {
                        battle.step(Time.time,Time.deltaTime);
                        for(int i=2;i<4;i++) if(battle.health[i]>0 && ZombieAnimation.choose_pose(battle,i,Time.time)==CharacterPose.Attack)attack_visible=true;
                        yield return null;
                    }
                    require(battle.health[0]==0 && battle.health[1]>0 && attack_visible,"actual front explosions kill first target and show attack");
                    float before=Vector3.Distance(battle.positions[67],battle.positions[1]);
                    // Explicit exhausted-path fixture: recovery must not require another gunshot or a moved target.
                    battle.crowd.agents[67].ResetPath(); battle.crowd.agents[67].isStopped=true;
                    until=Time.time+3;
                    while(Time.time<until) {battle.step(Time.time,Time.deltaTime);yield return null;}
                    float progress=before-Vector3.Distance(battle.positions[67],battle.positions[1]);
                    require(progress>3 && battle.geometry_errors==0 && battle.heard==0,"rear resumes silent chase after target death and exhausted path");
                    Debug.Log($"[ChaseRecoveryChecks] rear_progress={progress:F2} front_dead={battle.dead_zombies} first_target_dead=True no_gunshots=True attack_visible={attack_visible}");
                }
                finally {battle.try_supply_shot=null;}
            }
            yield return null;
        }

        private static IEnumerator check_building_contact()
        {
            var wall = new Bounds(new Vector3(0,1,0),new Vector3(6,2,6));
            using(var battle = new BattleSimulation(new[] {new Vector3(-80,0,-80),new Vector3(6,0,0)},1,new bool[2],new[] {wall},unit_ids:new[] {"firearm_infantry","brute"}))
            {
                var target = battle.add_building_target(wall,"Collision reach regression");
                float until = Time.time+8;
                while(Time.time<until && target.health==target.max_health)
                { battle.step(Time.time,Time.deltaTime); yield return null; }
                require(target.health<target.max_health && battle.geometry_errors==0,"large footprint building attack reach");
                Debug.Log("[CrowdSpacingChecks] PASS large-unit building contact");
            }
            yield return null;
        }

        private static IEnumerator check_slime()
        {
            Camera.main.transform.position = new Vector3(0,6,-6);
            Camera.main.transform.LookAt(Vector3.zero);
            Camera.main.orthographicSize = 3.5f;
            string[] args = Environment.GetCommandLineArgs();
            int output_index = Array.IndexOf(args,"-tripoOutput");
            string output = output_index >= 0 ? args[output_index+1] : Application.persistentDataPath;
            Directory.CreateDirectory(output);
            using (var feedback = new ExplosionFeedback(Shader.Find("Standard")))
            {
                var flashes = new[] { new BattleSimulation.Flash { origin = Vector3.zero, expires = Time.time+.85f } };
                float start = Time.time;
                int captures = 0;
                while(Time.time-start < 2.8f)
                {
                    feedback.draw_events(flashes,null,true);
                    if(captures==0 && Time.time-start>.25f || captures==1 && Time.time-start>1)
                        ScreenCapture.CaptureScreenshot(Path.Combine(output,$"slime-{++captures}.png"));
                    yield return null;
                }
                feedback.draw_events(flashes,null,true);
                require(feedback.emitted_splashes==1 && feedback.live_splashes==0,"slime deduplication / expiry");
            }
        }

        private static void require(bool condition, string label)
        {
            if(condition) return;
            Debug.LogError("[CrowdSpacingChecks] FAIL " + label);
            Application.Quit(2);
            throw new InvalidOperationException(label);
        }
    }
}
