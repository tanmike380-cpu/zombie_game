using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.AI;
using ZombieGame.World;
using ZombieGame.Balance;

namespace ZombieGame.FrontierTests
{
    public sealed class CoastalSmokeChecks:MonoBehaviour
    {
        private bool camera_fixture;
        private void LateUpdate(){if(camera_fixture){Camera.main.orthographicSize=34;GetComponent<FrontierGame>().focus_camera(new Vector3(-91,0,-93));}}
        private IEnumerator Start()
        {
            if(Array.IndexOf(Environment.GetCommandLineArgs(),"-coastalSmoke")<0)yield break;
            camera_fixture=true;
            yield return new WaitForSeconds(3);
            var game=GetComponent<FrontierGame>();
            require(game.current!=null&&game.current.living_soldiers==400,"400 deployed soldiers");
            require(game.current.zombie_count==2000,"2000 zombies");
            require(game.imported_roster.units.Length>=7,"real character roster");
            int walls=0;
            foreach(var building in game.current.buildings)
            {
                if(building.label.Contains("WALL"))walls++;
                require(!NavMesh.SamplePosition(new Vector3(building.bounds.center.x,0,building.bounds.center.z),out var hit,.1f,NavMesh.AllAreas),"building footprint blocked "+building.label);
            }
            require(walls>60,"walled camp");
            var wall_center=new Vector3(-109,0,-55);
            require(game.current.ranged_line_clear(wall_center+Vector3.back*2,wall_center+Vector3.forward*2),"ranged defenders fire across friendly parapet");
            require(!game.current.ranged_line_clear(game.map.headquarters_position+Vector3.left*8,game.map.headquarters_position+Vector3.right*8),"ordinary buildings still block fire");
            require(!NavMesh.SamplePosition(new Vector3(-95,0,-121),out var sea,.1f,NavMesh.AllAreas),"sea non-walkable");
            UnitBalance.validate(UnitBalance.config);
            require(ZombieGame.Controls.RtsCameraPan.frame_seconds(10)==.05f,"startup camera hitch bounded");
            require(Mathf.Abs(ZombieGame.Controls.RtsCameraPan.frame_seconds(.016f)-.016f)<.0001f,"normal edge pan unchanged");
            require(!FrontierHud.show_building_health(new ZombieGame.Combat.BattleBuilding{health=300,max_health=300}),"full building health hidden");
            require(FrontierHud.show_building_health(new ZombieGame.Combat.BattleBuilding{health=200,max_health=300}),"damaged building health visible");
            foreach(var binding in game.imported_roster.units)
            {
                var first=binding.frames.poses[1].frames[0];var middle=binding.frames.poses[1].frames[binding.frames.poses[1].frames.Length/4];
                var a=first.vertices;var b=middle.vertices;float movement=0;for(int i=0;i<a.Length;i++)movement+=Vector3.Distance(a[i],b[i]);
                require(movement>.01f,"non-static locomotion: "+binding.unit_id);
            }
            var folder=new DirectoryInfo(Application.dataPath);
            while(folder!=null&&!folder.Name.EndsWith(".app"))folder=folder.Parent;
            if(folder==null)throw new InvalidOperationException("Coastal smoke requires standalone app");
            string output=Path.Combine(folder.Parent.FullName,"ArtReview/CoastalDefense");
            Directory.CreateDirectory(output);
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(Path.Combine(output,"camp.png"));
            Debug.Log("[CoastalSmoke] STARTUP PASS walls="+walls+" shared balance="+UnitBalance.source_hash);
            // Separate smoke-only accelerated clock; regular player keeps the full 60-second preparation.
            game.current.order_siege(game.map.headquarters_position,game.current.zombie_count);
            float started=Time.realtimeSinceStartup;int frames=0;double simulation_ms=0,presentation_ms=0;
            while(Time.realtimeSinceStartup-started<35){frames++;simulation_ms+=game.last_simulation_ms;presentation_ms+=game.last_presentation_ms;yield return null;}
            yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot(Path.Combine(output,"battle.png"));
            int damaged=0;foreach(var building in game.current.buildings)if(building.health<building.max_health)damaged++;
            Debug.Log($"[CoastalSmoke] COMBAT soldiers={game.current.living_soldiers} dead_zombies={game.current.dead_zombies} shots={game.current.shots} damaged_buildings={damaged} geometry={game.current.geometry_errors} fps={frames/(Time.realtimeSinceStartup-started):F1}");
            Debug.Log($"[CoastalSmoke] SCRIPT simulation_ms={simulation_ms/frames:F1} presentation_ms={presentation_ms/frames:F1}");
            require(damaged>0,"siege reaches and damages defenses");
            require(game.current.hits>0,"defenders projectiles hit attackers");
            int breached=0;
            foreach(var building in game.current.buildings)
                if(building.infected&&building.label.Contains("WALL")&&NavMesh.SamplePosition(new Vector3(building.bounds.center.x,0,building.bounds.center.z),out var opening,.25f,NavMesh.AllAreas))breached++;
            require(breached>0,"destroyed wall opens native navigation");
            Debug.Log("[CoastalSmoke] BREACH PASS opened_wall_cells="+breached);
            require(game.current.geometry_errors==0,"no terrain intrusion");
            Debug.Log("[CoastalSmoke] COMPLETE");camera_fixture=false;yield return new WaitForSecondsRealtime(1);Application.Quit(0);
        }
        private static void require(bool passed,string message)
        {if(!passed){Debug.LogError("[CoastalSmoke] FAIL "+message);Application.Quit(1);throw new InvalidOperationException(message);}}
    }
}
