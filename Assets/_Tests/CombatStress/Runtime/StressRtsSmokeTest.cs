using System;
using System.Collections;
using UnityEngine;
using ZombieGame.Balance;

namespace ZombieGame.CombatStressTests
{
    public sealed class StressRtsSmokeTest : MonoBehaviour
    {
        public CombatStressBenchmark game;

        private IEnumerator Start()
        {
            yield return null;
            yield return verify_movement_speeds();
            game.reset_playable();
            yield return null;
            var s = game.current; var input = game.GetComponent<StressRtsInput>();
            require(s.playable && s.shots == 0 && s.ever_active == 0, "Not a safe manual start");
            require(input.selected_count() == 400, "Initial 400-unit selection");
            require(!s.issue_order(400,SoldierOrder.Move,Vector3.zero), "Enemy accepted player command");
            require(!s.issue_order(0,SoldierOrder.AttackTarget,Vector3.zero,400), "Hidden enemy accepted direct attack");
            require(!s.issue_order(0,SoldierOrder.Move,new Vector3(36,0,-53)), "Water accepted move");
            require(!s.issue_order(0,SoldierOrder.Move,new Vector3(140,0,0)), "Out-of-map accepted move");
            Vector3 start = s.positions[0];
            require(input.issue_selected(SoldierOrder.Move,input.selection_center()+Vector3.right*3) == 400, "Group move command rejected");
            for (int i=0;i<400;i++) require(!s.crowd.agents[i].pathPending && s.crowd.agents[i].hasPath, "Move path queued");
            yield return new WaitForSeconds(1.5f);
            require(s.positions[0].x > start.x + 2 && s.shots == 0, "Move/no auto-fire");
            input.issue_selected(SoldierOrder.Move,input.selection_center()+Vector3.right*10);
            yield return new WaitForSeconds(.2f);
            input.issue_selected(SoldierOrder.Stop,Vector3.zero);
            Vector3 stopped = s.positions[0];
            yield return new WaitForSeconds(.3f);
            require(Vector3.Distance(stopped,s.positions[0]) < .1f, "Stop drift");
            require(s.issue_order(0,SoldierOrder.Patrol,new Vector3(-97,0,-9)), "Patrol rejected");
            yield return new WaitForSeconds(.4f);
            require(s.orders[0] == SoldierOrder.Patrol && s.crowd.agents[0].velocity.x < 0, "Patrol direction");
            s.issue_order(0,SoldierOrder.Stop,Vector3.zero);

            Vector3 screen = Camera.main.WorldToScreenPoint(s.positions[1]+Vector3.up*.6f);
            Vector2 gui = new Vector2(screen.x,Screen.height-screen.y);
            require(input.pick_unit(gui,false)==1,"Screen-space unit picking");
            input.select_rectangle(gui+new Vector2(2,2),gui-new Vector2(2,2),false);
            require(input.selected_count()==1 && s.selected[1],"Click selection");
            input.select_rectangle(new Vector2(Screen.width,Screen.height),Vector2.zero,false);
            require(input.selected_count()==400,"Reverse box selection");
            input.store_group(1);
            for (int i=0;i<400;i++) s.selected[i] = i < 8;
            input.store_group(2);
            input.recall_group(1,false);
            require(input.selected_count()==400,"Group 1 recall");
            input.recall_group(2,true);
            require(input.selected_count()==8 && s.selected[7] && !s.selected[8],"Group 2 recall");
            require(Vector3.Distance(Camera.main.transform.position,new Vector3(input.selection_center().x,200,input.selection_center().z))<.01f,"Double-tap group camera focus");
            s.health[7]=0; input.recall_group(2,false);
            require(input.selected_count()==7,"Dead group member selected");
            s.health[7]=UnitBalance.human.health;
            input.clear_groups(); input.recall_group(2,false);
            require(input.selected_count()==0,"Stale groups after reset");
            input.select_all();
            var route = new UnityEngine.AI.NavMeshPath();
            require(UnityEngine.AI.NavMesh.CalculatePath(new Vector3(-75,0,0),new Vector3(-25,0,0),UnityEngine.AI.NavMesh.AllAreas,route)
                && route.status == UnityEngine.AI.NavMeshPathStatus.PathComplete && route.corners.Length>2,"Central obstacle detour missing");
            require(!s.issue_order(0,SoldierOrder.Move,new Vector3(-62,0,0)),"Central rock accepted move");
            input.issue_selected(SoldierOrder.Stop,Vector3.zero);
            input.select_all();
            for (int i=0;i<400;i++)
                require(s.crowd.agents[i].Warp(new Vector3(-115+i%20*.8f,0,(i<200 ? -40 : 32)+(i%200)/20*.8f)),"Split squad fixture warp");
            yield return null;
            Vector3 regroup = new Vector3(-107,0,0);
            require(input.issue_selected(SoldierOrder.Move,regroup)==400,"Split squads rejected regroup");
            for (int i=0;i<400;i++) require(Vector3.Distance(s.order_goals[i],regroup)<13,"Empty gap preserved in formation goals");
            float regroup_deadline = Time.time + 25;
            bool regrouped = false;
            while (Time.time < regroup_deadline)
            {
                regrouped = true;
                for (int i=0;i<400;i++) if (Vector3.Distance(s.positions[i],regroup)>13) regrouped = false;
                if (regrouped) break;
                yield return null;
            }
            require(regrouped,"Two separated squads failed to converge at center");
            input.issue_selected(SoldierOrder.Stop,Vector3.zero);
            Debug.Log("[RegroupSmoke] PASS 400 soldiers in two 200-unit squads, >64-tile empty gap removed; all reached within 13 tiles of click");

            require(s.crowd.agents[0].Warp(new Vector3(-100,0,-55)),"Fog fixture warp A");
            yield return new WaitForSeconds(.3f);
            require(game.current_fog.is_visible(new Vector3(-100,0,-55)),"Fog did not follow soldier");
            require(s.crowd.agents[0].Warp(new Vector3(-80,0,-55)),"Fog fixture warp B");
            yield return new WaitForSeconds(.3f);
            require(!game.current_fog.is_visible(new Vector3(-100,0,-55)) && game.current_fog.is_explored(new Vector3(-100,0,-55)),"Explored memory");
            require(game.current_fog.is_visible(new Vector3(-70.5f,0,-55)) && !game.current_fog.is_visible(new Vector3(-69,0,-55)),"Human 10-tile sight");

            require(s.crowd.agents[0].Warp(new Vector3(-15,0,0)),"Attack fixture soldier warp");
            s.crowd.agents[401].enabled = true;
            require(s.crowd.agents[401].Warp(new Vector3(-10,0,0)),"Attack fixture zombie warp");
            yield return new WaitForSeconds(.3f);
            require(s.issue_order(0,SoldierOrder.AttackTarget,s.positions[401],401),"Visible target rejected");
            yield return new WaitForSeconds(2.5f);
            require(s.shots>0 && s.hits>0 && s.ever_active>0,"Attack/noise chase loop");
            Debug.Log($"[StressRtsSmoke] PASS manual start, 400-unit move, stop, patrol, screen picking/box selection, groups/recall/focus/reset/dead filtering, central obstacle detour, terrain bounds, enemy/hidden rejection, moving fog/exploration, attack/noise chase. shots={s.shots} hits={s.hits} activated={s.ever_active}");
            game.reset_playable(); game.input_locked=false;
        }

        private IEnumerator verify_movement_speeds()
        {
            var simulation = game.current;
            int[] indices = { 0, 401, 400 };
            for (int lane=0;lane<indices.Length;lane++)
            {
                int index = indices[lane]; var agent = simulation.crowd.agents[index];
                agent.enabled = true;
                require(agent.Warp(new Vector3(-115,0,-100-lane*6)),"Speed fixture warp");
                require(Mathf.Approximately(agent.speed,simulation.stats_for(index).move_speed),"Agent ignored shared balance");
                if (index == 0) require(simulation.issue_order(index,SoldierOrder.Move,new Vector3(-85,0,-100)),"Human speed route");
                else require(simulation.crowd.investigate_position(index,new Vector3(-85,0,-100-lane*6)),"Zombie speed route");
            }
            yield return new WaitForSeconds(.5f);
            var starts = new Vector3[3];
            for (int i=0;i<3;i++) starts[i] = simulation.crowd.transforms[indices[i]].position;
            float begin = Time.time;
            yield return new WaitForSeconds(1);
            var measured = new float[3];
            for (int i=0;i<3;i++)
            {
                measured[i] = Vector3.Distance(starts[i],simulation.crowd.transforms[indices[i]].position)/(Time.time-begin);
                require(Mathf.Abs(measured[i]-simulation.stats_for(indices[i]).move_speed)<.25f,"Actual speed differs from balance: " + indices[i] + " measured=" + measured[i]);
            }
            require(measured[1]>measured[0] && measured[2]>measured[0],"Combat zombies not faster in actual movement");
            Debug.Log($"[SpeedSmoke] PASS measured tiles/s human={measured[0]:F3} runner={measured[1]:F3} exploder={measured[2]:F3} balance_hash={UnitBalance.source_hash}");
            foreach (int index in indices) { simulation.crowd.agents[index].isStopped=true; simulation.crowd.agents[index].ResetPath(); }
            require(simulation.crowd.agents[0].Warp(new Vector3(-105,0,-100)),"Chase human warp");
            require(simulation.crowd.agents[401].Warp(new Vector3(-108.5f,0,-100)),"Chase runner warp");
            require(simulation.issue_order(0,SoldierOrder.Move,new Vector3(-85,0,-100)),"Chase human move");
            yield return new WaitForSeconds(2);
            float gap = Vector3.Distance(simulation.crowd.transforms[0].position,simulation.crowd.transforms[401].position);
            require(gap<2.7f,"Runner failed to close on moving human: gap="+gap);
            Debug.Log($"[ChaseSmoke] PASS initial_gap=3.5 final_gap={gap:F3}");
        }

        private void require(bool condition,string message)
        {
            if (condition) return;
            game.input_locked=false;
            throw new InvalidOperationException("Stress RTS smoke: "+message);
        }
    }
}
