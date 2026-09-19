using System;
using System.Collections;
using UnityEngine;

namespace ZombieGame.CombatStressTests
{
    public sealed class StressRtsSmokeTest : MonoBehaviour
    {
        public CombatStressBenchmark game;

        private IEnumerator Start()
        {
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
            Debug.Log($"[StressRtsSmoke] PASS manual start, 400-unit move, stop, patrol, screen picking/box selection, terrain bounds, enemy/hidden rejection, moving fog/exploration, attack/noise chase. shots={s.shots} hits={s.hits} activated={s.ever_active}");
            game.reset_playable(); game.input_locked=false;
        }

        private void require(bool condition,string message)
        {
            if (condition) return;
            game.input_locked=false;
            throw new InvalidOperationException("Stress RTS smoke: "+message);
        }
    }
}
