using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using ZombieGame.World;
using ZombieGame.Balance;

namespace ZombieGame.FrontierTests
{
    /// <summary>Opt-in disposable full-world damage fixtures; never executed in ordinary play.</summary>
    public sealed class SettlementSmoke:MonoBehaviour
    {
        private void LateUpdate()
        {
            var game=FindFirstObjectByType<FrontierGame>();
            if(game?.current!=null)game.focus_camera(game.map.headquarters_position);
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void install()
        {
            if(Array.IndexOf(Environment.GetCommandLineArgs(),"-settlementSmoke")<0)return;
            new GameObject("Disposable settlement fixture").AddComponent<SettlementSmoke>();
            Application.logMessageReceived+=(message,trace,type)=>{if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)Application.Quit(1);};
        }
        private static void require(bool condition,string message){if(!condition)throw new InvalidOperationException(message);}
        private IEnumerator Start()
        {
            yield return null;var game=FindFirstObjectByType<FrontierGame>();
            check_resources(game.map);check_recruits(game);
            // Fixture ceasefire: only this process, no authored stat/health mutation.
            game.current.try_supply_shot=_=>false;
            int walker=-1;
            for(int i=game.current.soldier_count;i<game.current.total_count;i++)if(game.current.health[i]>0&&game.current.stats_for(i).id=="walker"){walker=i;break;}
            var headquarters=game.structures.headquarters;
            Vector3 position=new Vector3(headquarters.bounds.min.x-1,0,headquarters.bounds.center.z);
            var agent=game.current.crowd.agents[walker];agent.enabled=true;
            require(agent.Warp(position),"fixture walker warp beside HQ");agent.isStopped=true;
            yield return new WaitForSeconds(2.5f);
            Debug.Log($"[SettlementSmoke] attack fixture hp={headquarters.health} zombie={game.current.positions[walker]} path={agent.hasPath} stopped={agent.isStopped}");
            require(headquarters.health<500&&headquarters.health>0,"ordinary zombie actually damages HQ");
            int expected=0;
            foreach(var building in game.current.buildings)
            {
                require(building.max_health==(building.headquarters?500:300),"shared authored building health");
                expected+=building.headquarters?30:10;
                game.current.damage_building(building,building.health,Time.time);
                game.current.damage_building(building,100,Time.time); // Must not duplicate an infection burst.
            }
            float deadline=Time.time+8;
            while(game.current.infection_spawned<expected&&Time.time<deadline)yield return null;
            require(game.current.infection_spawned==expected,"exact 10/30 infection burst, no duplicate");
            require(game.current.zombie_count==FrontierMap.ZOMBIES+expected,"infection population counted");
            require(game.defeated&&!game.can_control&&!game.production.operational,"HQ defeat stops production/commands, simulation continues");
            require(game.supply.depots.Count==0&&game.economy.powder_workshops==0&&game.economy.arrow_workshops==0,"lost facilities stop supplies and manufacturing");
            require(!game.production.enqueue(0,100),"cannot recruit after defeat");
            require(game.current.geometry_errors==0,"infection spawns outside blocked footprints");
            Debug.Log($"[SettlementSmoke] PASS resource tiers/yields/six recruits/building attacks/HP300-500/infection={expected}/defeat/no duplicate/no geometry errors");
            game.current.try_supply_shot=null;
            ScreenCapture.CaptureScreenshot("/tmp/zombie-settlement-defeat.png");yield return new WaitForSeconds(.3f);Application.Quit(0);
        }
        private static void check_resources(FrontierMap map)
        {
            var config=ResourceDistribution.load();var terrain=new ResourceTerrain(map);var counts=new int[3,4];
            string[] kinds={"Food","Wood","Stone","Iron"};
            foreach(var site in map.resource_sites)
            {
                var tier=config.tiers[site.tier-1];float distance=Vector3.Distance(site.position,tier.center_on_map?Vector3.zero:map.headquarters_position);
                require(distance>=tier.min_distance&&distance<=tier.max_distance,"circular tier distances");
                counts[site.tier-1,Array.IndexOf(kinds,site.kind)]++;
                require(terrain.resource_multiplier(site.kind.ToLowerInvariant(),site.position)==tier.yield_multiplier,"real harvesting multiplier");
                var recipe=new HeadquartersRecipe{id=site.kind.ToLowerInvariant(),gather_radius=site.radius,yield_per_cell_minute=1};
                float rate=terrain.estimate(recipe,site.position,new HashSet<int>(),out var cells);
                require(cells.Length>0&&rate>0,"all deposits actually harvestable");
                require(terrain.estimate(recipe,site.position,new HashSet<int>(cells),out _)==0,"claimed cells cannot double yield");
            }
            for(int tier=0;tier<3;tier++)for(int kind=0;kind<4;kind++)require(counts[tier,kind]==config.tiers[tier].sites_per_kind,"all four raw resources at every tier");
        }
        private static void check_recruits(FrontierGame game)
        {
            int before=game.current.living_soldiers,slot=FrontierMap.SOLDIERS;
            foreach(var recipe in game.production.config.recipes)
            {
                if(!recipe.is_unit)continue;
                require(game.finish_production(recipe),"recruit "+recipe.recruit_id);
                var stats=UnitBalance.get(recipe.recruit_id);
                require(game.current.stats_for(slot)==stats&&game.current.health[slot]==stats.health&&game.current.crowd.agents[slot].speed==stats.move_speed,"typed recruit reads authored stats");
                game.current.ammunition[slot]=0;float powder=game.economy.gunpowder,arrows=game.economy.arrows;
                game.supply.step(game.current);
                require(game.current.ammunition[slot]==stats.ammunition_capacity,"typed refill capacity");
                require(stats.ammunition_type=="arrows"?game.economy.arrows==arrows-stats.ammunition_capacity&&game.economy.gunpowder==powder:game.economy.gunpowder==powder-stats.ammunition_capacity&&game.economy.arrows==arrows,"arrows and powder remain distinct");
                slot++;
            }
            require(game.current.living_soldiers==before+6,"six non-melee human roles");
        }
    }
}
