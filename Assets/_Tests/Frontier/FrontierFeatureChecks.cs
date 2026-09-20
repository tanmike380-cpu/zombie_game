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
            check_focus(game);check_resources(game);check_ammunition(game.economy.config);check_placement(game);
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
                fixture.step(.7f,.1f);require(fixture.melee_strikes==1,"knife cooldown before 1 second");
                fixture.step(1.21f,.1f);require(fixture.melee_strikes==2&&fixture.shots==0,"knife interval 1 / no gunshots");
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
