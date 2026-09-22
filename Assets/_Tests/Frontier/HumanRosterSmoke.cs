using System;
using UnityEngine;
using ZombieGame.Balance;
using ZombieGame.Combat;
using ZombieGame.World;

namespace ZombieGame.FrontierTests
{
    /// <summary>Isolated single-volley fixture: no artificial unit numbers or balance overrides.</summary>
    public sealed class HumanRosterSmoke:MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void install()
        {
            if(Array.IndexOf(Environment.GetCommandLineArgs(),"-humanRosterSmoke")<0)return;
            var game=FindFirstObjectByType<FrontierGame>();if(game!=null)game.enabled=false;
            new GameObject("Disposable human roster fixture").AddComponent<HumanRosterSmoke>();
        }
        private static void require(bool condition,string message){if(!condition)throw new InvalidOperationException(message);}
        private void Start()
        {
            try
            {
                var spawns=new Vector3[13];var ids=new string[13];
                for(int i=0;i<6;i++){spawns[i]=new Vector3(-100+i*40,0,0);spawns[i+6]=spawns[i]+Vector3.forward*3;ids[i]=UnitBalance.human_ids[i];ids[i+6]="brute";}
                spawns[12]=spawns[11]+Vector3.right*1.5f;ids[12]="brute";
                using(var fixture=new BattleSimulation(spawns,6,new bool[13],Array.Empty<Bounds>(),player_controlled:false,unit_ids:ids))
                {
                    fixture.step(0,1); // One discrete simulation tick; all projectiles resolve their first volley.
                    require(fixture.shots==6&&fixture.hits==6,"all six roles fire real projectiles");
                    for(int i=0;i<6;i++)
                    {
                        var stats=UnitBalance.get(ids[i]);float damage=stats.damage*Mathf.Max(1,stats.large_target_multiplier);
                        require(Mathf.Abs(fixture.health[i+6]-(UnitBalance.get("brute").health-damage))<.01f,"authored damage "+ids[i]);
                        require(fixture.ammunition[i]==stats.ammunition_capacity-stats.ammunition_cost,"authored ammo cost "+ids[i]);
                    }
                    require(fixture.health[12]==UnitBalance.get("brute").health-UnitBalance.get("cannon").damage,"cannon damages second target in splash radius");
                    require(fixture.dropped_projectiles==0,"no dropped projectiles");
                    foreach(var alert in fixture.attack_alerts)require(alert.expires==0,"hitting zombies does not warn the player about friendly damage");
                }
                ZombieGame.CombatStressTests.NoiseRetargetChecks.run();
                Debug.Log("[HumanRosterSmoke] PASS six projectiles/authored damage/ballista large bonus/cannon AOE/distinct ammo costs/noise regressions");Application.Quit(0);
            }
            catch(Exception exception){Debug.LogError("[HumanRosterSmoke] FAIL "+exception);Application.Quit(1);}
        }
    }
}
