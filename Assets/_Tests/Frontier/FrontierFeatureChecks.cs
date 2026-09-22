using System;
using System.Collections.Generic;
using UnityEngine;
using ZombieGame.World;
using ZombieGame.Controls;
using ZombieGame.Combat;
using ZombieGame.Balance;

namespace ZombieGame.FrontierTests
{
    /// <summary>Disposable smoke fixtures; never attached to manual play without -frontierSmoke.</summary>
    public static class FrontierFeatureChecks
    {
        private static void require(bool success,string message)
        {if(!success)throw new InvalidOperationException(message);}
        public static void run(FrontierGame game)
        {
            check_focus(game);check_minimap(game);check_resources(game);check_ammunition(game.economy.config);check_placement(game);
        }
        private static void check_focus(FrontierGame game)
        {
            var positions=new Vector3[30];var health=new float[30];var selected=new bool[30];
            for(int i=0;i<30;i++){positions[i]=i<24?new Vector3(i%6,0,i/6):new Vector3(50+i*2,0,60);health[i]=100;selected[i]=true;}
            require(SelectionFocus.find(positions,health,selected,30).x<6,"main cluster beats distant reinforcements");
            for(int i=0;i<24;i++)health[i]=0;
            require(SelectionFocus.find(positions,health,selected,30).x>=98,"focus excludes dead main army");
            game.focus_camera(Vector3.zero);game.controls.press_select_all(10);
            require(game.camera_focus==Vector3.zero,"single all key selects without camera jump");
            game.controls.press_select_all(10.2f);
            require(Vector3.Distance(game.camera_focus,game.controls.selection_center())<.01f,"double F2/backquote focuses cluster");
            game.controls.store_group(1);game.focus_camera(Vector3.zero);game.controls.recall_group(1,true);
            require(Vector3.Distance(game.camera_focus,game.controls.selection_center())<.01f,"double group focus uses cluster");
            Debug.Log("[DenseFocusSmoke] PASS main cluster/dead filtering/single select/double all/group recall");
        }
        private sealed class CameraOnlyFixture:IRtsBattleView
        {
            private readonly FrontierGame source;
            public CameraOnlyFixture(FrontierGame source){this.source=source;}
            public BattleSimulation current=>source.current;
            public ZombieGame.Vision.CombatFog current_fog=>source.current_fog;
            public bool can_control=>false;
            public bool reveal_map=>source.reveal_map;
            public Vector3 camera_focus=>source.camera_focus;
            public void focus_camera(Vector3 point)=>source.focus_camera(point);
            public bool pointer_over_ui(Vector2 point)=>source.pointer_over_ui(point);
            public Color32 obstacle_color(int index)=>source.obstacle_color(index);
        }
        private static void check_minimap(FrontierGame game)
        {
            var controls=game.controls;var original_view=controls.game;var original_focus=game.camera_focus;
            var original_intercept=controls.intercept_world_input;int selected=controls.selected_count();
            bool original_panel=controls.custom_command_panel;
            try
            {
                foreach(bool custom in new[]{true,false})
                {
                    controls.custom_command_panel=custom;var area=controls.minimap_bounds;
                    controls.cancel_command();
                    controls.intercept_world_input=_=>throw new InvalidOperationException("Minimap leaked into building/world input");
                    controls.handle_mouse(new Event{type=EventType.MouseDown,button=0,mousePosition=area.center});
                    require(controls.minimap_dragging&&game.camera_focus.sqrMagnitude<.01f,"minimap left down captures camera");
                    for(int i=1;i<=4;i++)
                    {
                        controls.handle_mouse(new Event{type=EventType.MouseDrag,button=0,mousePosition=area.center+new Vector2(area.width*i*.1f,-area.height*i*.1f)});
                        require(Vector3.Distance(game.camera_focus,new Vector3(25.6f*i,0,25.6f*i))<.01f,"continuous camera drag");
                    }
                    var outside=new Vector2(area.xMax+30,area.y-30);
                    controls.handle_mouse(new Event{type=EventType.MouseDrag,button=0,mousePosition=outside});
                    require(game.camera_focus==new Vector3(128,0,128),"capture clamps outside map");
                    controls.handle_mouse(new Event{type=EventType.MouseUp,button=0,mousePosition=outside});
                    require(!controls.minimap_dragging&&controls.selected_count()==selected,"release outside preserves selection");
                    controls.handle_mouse(new Event{type=EventType.MouseDown,button=0,mousePosition=area.center});
                    controls.SendMessage("OnApplicationFocus",false);require(!controls.minimap_dragging,"focus loss cancels capture");
                    controls.game=new CameraOnlyFixture(game);
                    controls.handle_mouse(new Event{type=EventType.MouseDown,button=0,mousePosition=area.center});
                    controls.handle_mouse(new Event{type=EventType.MouseDrag,button=0,mousePosition=area.center+Vector2.right*area.width*.25f});
                    require(Mathf.Abs(game.camera_focus.x-64)<.01f&&controls.minimap_dragging,"camera works when orders are disabled: "+game.camera_focus);
                    controls.handle_mouse(new Event{type=EventType.MouseUp,button=0,mousePosition=area.center});
                    controls.game=original_view;
                    controls.handle_mouse(new Event{type=EventType.MouseDown,button=1,mousePosition=area.center});
                    require(!controls.minimap_dragging&&game.current.orders[0]==SoldierOrder.Move,"right-click remains movement");
                    controls.arm("Attack");controls.handle_mouse(new Event{type=EventType.MouseDown,button=0,mousePosition=area.center});
                    require(!controls.minimap_dragging&&game.current.orders[0]==SoldierOrder.AttackMove,"A-left remains attack move");
                }
            }
            finally
            {
                controls.game=original_view;controls.custom_command_panel=original_panel;controls.intercept_world_input=original_intercept;
                controls.cancel_pointer_capture();controls.cancel_command();controls.issue_selected(SoldierOrder.Stop,Vector3.zero);game.focus_camera(original_focus);
            }
            Debug.Log("[MinimapSmoke] PASS both layouts/click/continuous drag/outside clamp/release/focus loss/disabled orders/selection/right move/A attack");
        }
        private static void check_resources(FrontierGame game)
        {
            var terrain=new ResourceTerrain(game.map);var recipe=game.production.config.recipes[2];
            float rate=terrain.estimate(recipe,new Vector3(-89,0,-40),new HashSet<int>(),out int[] cells);
            require(cells.Length>0&&Mathf.Abs(rate-cells.Length*10)<.01f,"each unique forest tile yields 10 wood/minute");
            require(terrain.estimate(recipe,new Vector3(-89,0,-40),new HashSet<int>(cells),out var repeated)==0,"no duplicate tree yield");
            float low=1,high=0;
            for(int x=-120;x<120;x+=10)for(int z=-120;z<120;z+=10){float f=terrain.fertility(new Vector3(x,0,z));low=Mathf.Min(low,f);high=Mathf.Max(high,f);}
            require(high-low>.2f,"fertility changes expected food output");
            Debug.Log($"[TerrainYieldSmoke] PASS unique woodland tiles={cells.Length} wood/min={rate} fertility={low:F2}..{high:F2}");
        }
        private static void check_ammunition(FrontierEconomyConfig config)
        {
            var economy=new FrontierEconomy(config);var supply=new AmmunitionSupply(economy);supply.depots.Add(Vector3.zero);supply.depots.Add(Vector3.right);
            // Separate short-lived simulation: health/position/ammo fixtures never touch manual gameplay.
            using(var fixture=new BattleSimulation(new[]{new Vector3(-5,0,0),new Vector3(-.5f,0,0)},1,new bool[2],Array.Empty<Bounds>()))
            {
                fixture.ammunition[0]=0;economy.gunpowder=7;supply.step(fixture);
                require(fixture.ammunition[0]==7&&economy.gunpowder==0,"limited stock transfer / overlapping depots no duplication");
                fixture.ammunition[0]=0;fixture.positions[0]=new Vector3(22,0,0);economy.gunpowder=40;supply.step(fixture);
                require(fixture.ammunition[0]==0&&economy.gunpowder==40,"outside radius cannot use global ammo");
                fixture.positions[0]=new Vector3(-20,0,0);supply.step(fixture);
                require(fixture.ammunition[0]==30&&economy.gunpowder==10,"20 tile boundary and 30-round capacity");
                fixture.ammunition[0]=0;fixture.positions[0]=new Vector3(-5,0,0);
                require(fixture.issue_order(0,SoldierOrder.AttackTarget,fixture.positions[1],1),"dry infantry can chase");
                fixture.step(0,.1f);require(fixture.crowd.agents[0].hasPath&&!fixture.crowd.agents[0].isStopped,"empty gun does not idle at gun range");
                fixture.crowd.agents[1].enabled=true;require(fixture.crowd.agents[1].Warp(new Vector3(-4.1f,0,0)),"melee fixture warp");fixture.crowd.agents[1].isStopped=true;
                fixture.step(.2f,.1f);require(fixture.melee_strikes==1&&fixture.health[1]==UnitBalance.runner.health-10,"knife deals 10");
                int attack_alerts=0;foreach(var alert in fixture.attack_alerts)if(alert.expires>.2f)attack_alerts++;
                require(fixture.health[0]<fixture.stats_for(0).health&&attack_alerts==1,"human bitten produces one minimap alarm");
                fixture.step(.7f,.1f);require(fixture.melee_strikes==1,"knife cooldown before 1 second");
                fixture.step(1.21f,.1f);require(fixture.melee_strikes==2&&fixture.shots==0,"knife interval 1 / no gunshots");
                attack_alerts=0;foreach(var alert in fixture.attack_alerts)if(alert.expires>1.21f)attack_alerts++;
                require(attack_alerts==1,"nearby repeated damage merges warnings");
                foreach(var alert in fixture.attack_alerts)require(alert.expires<10,"damage warnings expire instead of sticking forever");
                fixture.ammunition[0]=3;fixture.manual_melee[0]=true;require(fixture.uses_melee(0),"manual knife overrides loaded musket");
                fixture.manual_melee[0]=false;require(!fixture.uses_melee(0),"manual switch back to gun");
                fixture.step(2.3f,.1f);require(fixture.shots==1&&fixture.ammunition[0]==2&&economy.gunpowder==10,"shot consumes carried round, not distant global inventory");
            }
            Debug.Log("[AmmoMeleeSmoke] PASS 20 radius/30 carried/shared stock/no remote refill/auto chase/10 damage/1s cooldown/manual switch");
        }
        private static void check_placement(FrontierGame game)
        {
            game.current_fog.explore_area(new Rect(-128,-128,256,256));
            for(int index=1;index<game.production.config.recipes.Length;index++)
            {
                var recipe=game.production.config.recipes[index];bool found=false;Vector3 point=Vector3.zero;
                if(recipe.is_unit)continue;
                for(int z=-122;z<122&&!found;z+=4)for(int x=-122;x<122&&!found;x+=4)
                {
                    var candidate=new Vector3(x,0,z);
                    if(game.construction.validate(recipe,candidate,out _,out _,out _)){point=candidate;found=true;}
                }
                require(found,"find legal placement for "+recipe.id);
                require(game.construction.try_place(index,point),"place "+recipe.id);
                require(!game.construction.try_place(index,point),"overlap rejected");
                float rate=game.production.queue[0].yield_per_minute;
                game.production.step(recipe.seconds,game.finish_production);
                require(game.production.queue.Count==0,"complete placed facility");
                require(game.construction.facilities[index-1].complete,"completed visible model");
                if(recipe.id=="wood")require(Mathf.Abs(game.economy.wood_bonus_minute-rate)<.01f,"preview matches actual yield");
            }
            require(game.supply.depots.Count==2,"completed depot supplies, ghost does not");
            var depot=game.production.config.recipes[7];
            require(!game.construction.validate(depot,new Vector3(128,0,128),out _,out _,out _),"map bounds rejected");
            require(!game.construction.validate(depot,game.map.headquarters_position,out _,out _,out _),"existing building rejected");
            bool cancelled=false;
            for(int z=-118;z<118&&!cancelled;z+=4)for(int x=-118;x<118&&!cancelled;x+=4)
            {
                var point=new Vector3(x,0,z);
                if(!game.construction.validate(depot,point,out _,out _,out _))continue;
                int walls=game.current.crowd.walls.Length,regions=game.map.regions.Count,count=game.construction.facilities.Count;
                float wood=game.economy.wood;
                require(game.construction.try_place(7,point),"cancel fixture placement");
                game.production.cancel_last();
                require(game.current.crowd.walls.Length==walls&&game.map.regions.Count==regions&&game.construction.facilities.Count==count&&game.economy.wood==wood,"cancel removes footprint/model and refunds resource");
                cancelled=true;
            }
            require(cancelled,"cancel fixture found space");
            Debug.Log("[PlacementSmoke] PASS all seven distinct facilities/free placement/overlap/bounds/shared preview yield/completed depot");
        }
    }
}
