using System;
using System.Collections;
using UnityEngine;
using ZombieGame.Combat;
using ZombieGame.Balance;
using ZombieGame.CharacterTests;

namespace ZombieGame.FrontierTests
{
    public sealed class CoastalRegressionChecks:MonoBehaviour
    {
        private IEnumerator Start()
        {
            Application.runInBackground=true;Application.targetFrameRate=60;
            try{CoastalArtChecks.run();}
            catch(Exception error){Debug.LogException(error);Application.Quit(1);yield break;}
            yield return check_defenses();
            yield return CoastalOrderChecks.run();
            yield return CrowdSpacingChecks.run();
            Debug.Log("[CoastalRegression] PASS defenses, cone, ammo, frozen contacts and chase recovery");
            Application.Quit(0);
        }
        private static IEnumerator check_defenses()
        {
            var points=new[]{Vector3.zero,new Vector3(0,0,4),new Vector3(1,0,4),new Vector3(0,0,-4)};
            var ids=new[]{"greek_fire","walker","walker","walker"};
            using(var battle=new BattleSimulation(points,1,new bool[4],Array.Empty<Bounds>(),unit_ids:ids))
            {
                var stats=UnitBalance.get("greek_fire");
                require(BattleSimulation.inside_flame(Vector3.zero,Vector3.forward,points[1],stats),"cone front");
                require(!BattleSimulation.inside_flame(Vector3.zero,Vector3.forward,points[3],stats),"cone excludes rear");
                foreach(string label in new[]{"STONE WALL","FORTRESS WALL","FORTRESS WALL SIDE","WOODEN WALL","GATE TOWER","FIRE TOWER","箭塔","炮塔","石墙","木墙"})
                {
                    var structure=battle.add_building_target(new Bounds(new Vector3(40,1,40),Vector3.one*2),label);
                    battle.damage_building(structure,structure.max_health,Time.time);
                    require(structure.health==0&&!structure.infected&&structure.infection_remaining==0,"collapse without infection: "+label);
                }
                var house=battle.add_building_target(new Bounds(new Vector3(40,1,40),Vector3.one*2),"HOUSE");
                battle.damage_building(house,house.max_health,Time.time);
                require(house.infected&&house.infection_remaining==UnitBalance.config.buildings.normal_infection_count,"normal building still infects");
                house.infection_remaining=0; // Explicit disposable infection fixture; do not spawn an unrelated cohort.
                battle.issue_order(0,SoldierOrder.AttackTarget,points[1],1);
                int ammo=battle.ammunition[0];battle.step(Time.time,.016f);
                require(battle.flame_hits>=2&&battle.ammunition[0]==ammo-stats.ammunition_cost,"real AOE and per-pulse carried ammo");
                battle.ammunition[0]=0;battle.manual_melee[0]=true;
                require(!battle.uses_melee(0),"empty engine never becomes knife infantry");
                float health=battle.health[1];battle.step(Time.time+stats.attack_interval+.2f,.016f);
                require(battle.health[1]==health,"empty engine stops damage");
                var cannon=battle.add_building_target(new Bounds(new Vector3(0,1,8),Vector3.one*2),"GATE TOWER");cannon.weapon_id="cannon_bastion";
                var fire=battle.add_building_target(new Bounds(new Vector3(1,1,8),Vector3.one*2),"FIRE TOWER");fire.weapon_id="flame_bastion";
                int stock=10;battle.try_supply_defense_shot=weapon=>{if(stock<weapon.ammunition_cost)return false;stock-=weapon.ammunition_cost;return true;};
                battle.step(Time.time+1,.016f);
                require(battle.defense_shots==2&&stock==10-UnitBalance.get("cannon_bastion").ammunition_cost-UnitBalance.get("flame_bastion").ammunition_cost,"both tower types fire and pay global ammo");
                battle.damage_building(cannon,cannon.health,Time.time);battle.damage_building(fire,fire.health,Time.time);
                battle.step(Time.time+4,.016f);require(battle.defense_shots==2,"destroyed towers stop firing");
            }
            yield return null;
            Debug.Log("[CoastalRegression] PASS wall/tower destruction, ordinary infection, flame cone/ammo");
        }
        private static void require(bool condition,string message)
        {if(!condition){Debug.LogError("[CoastalRegression] FAIL "+message);Application.Quit(1);throw new InvalidOperationException(message);}}
    }
}
