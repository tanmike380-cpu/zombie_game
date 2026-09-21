using System;
using System.Collections;
using UnityEngine;
using UnityEngine.AI;
using ZombieGame.Balance;
using ZombieGame.Combat;
using ZombieGame.Controls;
using ZombieGame.World;

namespace ZombieGame.FrontierTests
{
    /// <summary>Opt-in isolated disposable combat fixture. No balance mutations; no normal game startup.</summary>
    public sealed class ZombieRosterSmoke:MonoBehaviour
    {
        private BattleSimulation fixture;
        private const int HUMANS=9;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void install_fixture()
        {
            if(Array.IndexOf(Environment.GetCommandLineArgs(),"-rosterSmoke")<0)return;
            var game=FindFirstObjectByType<FrontierGame>();if(game!=null)game.enabled=false;
            new GameObject("Disposable roster fixture").AddComponent<ZombieRosterSmoke>();
            Application.logMessageReceived+=fail_on_error;
        }
        private static void fail_on_error(string message,string trace,LogType type)
        {if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)Application.Quit(1);}
        private IEnumerator Start()
        {
            Application.runInBackground=true;Application.targetFrameRate=60;
            check_camera_edges();
            int settings_before=NavMesh.GetSettingsCount();
            var spawns=new Vector3[HUMANS+8];var ids=new string[HUMANS+8];var explosive=new bool[HUMANS+8];
            for(int i=0;i<8;i++)
            {
                spawns[i]=new Vector3(-84+i*24,0,0);spawns[i+HUMANS]=spawns[i]+Vector3.forward*4;
                ids[i+HUMANS]=UnitBalance.zombie_ids[i];explosive[i+HUMANS]=ids[i+HUMANS]=="exploder";
            }
            spawns[8]=new Vector3(83.1f,0,.2f); // A second soldier inside the boss slam, outside body clearance.
            // A 1.2-tile passage admits small units, but excludes giant/boss clearance profiles.
            var walls=new[]{new Bounds(new Vector3(0,1,64.3f),new Vector3(2,2,127.4f)),new Bounds(new Vector3(0,1,-64.3f),new Vector3(2,2,127.4f))};
            fixture=new BattleSimulation(spawns,HUMANS,explosive,walls,unit_ids:ids);
            fixture.try_supply_shot=_=>false; // Fixture-only ceasefire gate; does not mutate shared balance or ammo.
            for(int i=0;i<HUMANS;i++)
            {
                // Named fixture hold: move order at the current position suppresses human attacks each tick.
                fixture.orders[i]=SoldierOrder.Move;
                fixture.order_goals[i]=fixture.positions[i];
            }
            check_clearance();
            check_boss_markers();
            float end=Time.time+1.4f;
            while(Time.time<end)
            {
                for(int i=0;i<HUMANS;i++){fixture.orders[i]=SoldierOrder.Move;fixture.order_goals[i]=fixture.positions[i];}
                fixture.step(Time.time,Time.deltaTime);yield return null;
            }
            for(int i=HUMANS;i<fixture.total_count;i++)require(fixture.activated[i],"all zombie types see and activate");
            require(fixture.acid_shots>0,"spitter fires real projectile");
            require(fixture.slam_attacks>0,"giant/boss executes authored area attack");
            require(fixture.health[7]==0&&fixture.health[8]==0,"boss slam hits more than its primary target");
            int spitter_target=Array.IndexOf(UnitBalance.zombie_ids,"spitter");
            float after_hit=fixture.health[spitter_target];
            require(after_hit<UnitBalance.human.health,"acid projectile causes damage");
            fixture.crowd.agents[spitter_target+HUMANS].enabled=false;fixture.health[spitter_target+HUMANS]=0; // Disposable source-death fixture.
            float poison_end=Time.time+.25f;
            while(Time.time<poison_end){fixture.step(Time.time,Time.deltaTime);yield return null;}
            require(fixture.health[spitter_target]<after_hit,"poison continues after source dies");
            require(fixture.shots==0,"fixture never fires human guns");
            require(fixture.geometry_errors==0,"enemy movement respects map walls");
            Debug.Log("[ZombieRosterSmoke] PASS eight roles/sight/shared stats/spit/dot/slam/native size clearance/edge pan");
            fixture.try_supply_shot=null;fixture.Dispose();fixture=null;
            require(NavMesh.GetSettingsCount()==settings_before,"runtime large-unit profiles cleaned up");Application.Quit(0);
        }
        private void check_clearance()
        {
            var path=new NavMeshPath();
            foreach(int index in new[]{0,HUMANS+6,HUMANS+7})
            {
                var filter=new NavMeshQueryFilter{agentTypeID=fixture.crowd.agents[index].agentTypeID,areaMask=NavMesh.AllAreas};
                bool route=NavMesh.CalculatePath(new Vector3(-8,0,0),new Vector3(8,0,0),filter,path)&&path.status==NavMeshPathStatus.PathComplete;
                require(route==(index==0),"small passage differs for boss clearance");
            }
            for(int i=0;i<fixture.total_count;i++)require(Mathf.Approximately(fixture.crowd.agents[i].radius,UnitBalance.navigation_radius(fixture.stats_for(i))),"authored navigation radius");
            require(UnitBalance.get("boss").map_revealed&&UnitBalance.get("boss").model_scale>=4,"large globally marked boss");
        }
        private void check_boss_markers()
        {
            var pixels=new Color32[256*256];int boss=HUMANS+7;
            Vector3 p=fixture.positions[boss];int pixel=Mathf.FloorToInt(p.x+128)+Mathf.FloorToInt(p.z+128)*256;
            BossMapMarkers.draw(fixture,pixels);
            require(pixels[pixel].a==255,"boss appears over undiscovered map");
            int marked=0;foreach(var color in pixels)if(color.a!=0)marked++;
            require(marked==41,"only compact boss diamond revealed, not surrounding units/terrain");
            float original=fixture.health[boss];
            try
            {
                fixture.health[boss]=0;Array.Clear(pixels,0,pixels.Length);BossMapMarkers.draw(fixture,pixels);
                require(pixels[pixel].a==0,"dead boss marker removed");
            }
            finally{fixture.health[boss]=original;}
        }
        private static void check_camera_edges()
        {
            var size=new Vector2(1600,1000);
            require(RtsCameraPan.edge_direction(new Vector2(1,500),size,true)==Vector2.left,"left edge");
            require(RtsCameraPan.edge_direction(new Vector2(1599,500),size,true)==Vector2.right,"right edge");
            require(RtsCameraPan.edge_direction(new Vector2(800,999),size,true)==Vector2.up,"top edge");
            for(int frame=0;frame<300;frame++)
            {
                require(RtsCameraPan.edge_direction(new Vector2(1600,500),size,true)==Vector2.right,"sustained exact right boundary");
                require(RtsCameraPan.edge_direction(new Vector2(800,1000),size,true)==Vector2.up,"sustained exact top boundary");
            }
            require(RtsCameraPan.edge_direction(new Vector2(1601,500),size,true)==Vector2.zero,"outside right boundary");
            require(RtsCameraPan.edge_direction(new Vector2(800,1001),size,true)==Vector2.zero,"outside top boundary");
            require(RtsCameraPan.edge_direction(new Vector2(800,1),size,true)==Vector2.down,"bottom edge over HUD");
            require(Mathf.Approximately(RtsCameraPan.edge_direction(Vector2.one,size,true).magnitude,1),"diagonal not faster");
            require(RtsCameraPan.edge_direction(new Vector2(-1,500),size,true)==Vector2.zero,"outside viewport");
            require(RtsCameraPan.edge_direction(Vector2.one,size,false)==Vector2.zero,"unfocused viewport");
            require(RtsCameraPan.edge_direction(size*.5f,size,true)==Vector2.zero,"centre does not pan");
            Vector3 direction=RtsCameraPan.world_direction(Vector2.up,Quaternion.Euler(45,30,0));
            require(direction.x>0&&direction.z>0&&Mathf.Abs(direction.y)<.001f,"pan follows rotated camera plane");
        }
        private static void require(bool success,string message)
        {if(!success)throw new InvalidOperationException("[ZombieRosterSmoke] "+message);}
        private void OnDestroy(){fixture?.Dispose();Application.logMessageReceived-=fail_on_error;}
    }
}
