using System;
using System.Collections;
using UnityEngine;
using ZombieGame.Combat;
using ZombieGame.Controls;
using ZombieGame.Presentation;
using ZombieGame.World;

namespace ZombieGame.FrontierTests
{
    public sealed class UnitUiSmoke:MonoBehaviour
    {
        private BattleSimulation fixture;
        private UnitFeedbackOverlay overlay;
        private CharacterCrowdRenderer models;
        private RtsCursor cursor;
        private Camera camera_view;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void install_fixture()
        {
            if(Array.IndexOf(Environment.GetCommandLineArgs(),"-unitUiSmoke")<0)return;
            var game=FindFirstObjectByType<FrontierGame>();if(game!=null)game.enabled=false;
            new GameObject("Disposable unit UI fixture").AddComponent<UnitUiSmoke>();
            Application.logMessageReceived+=fail_on_error;
        }
        private static void fail_on_error(string message,string trace,LogType type)
        {if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)Application.Quit(1);}
        private IEnumerator Start()
        {
            Application.runInBackground=true;camera_view=Camera.main;
            camera_view.transform.position=new Vector3(0,20,-20);camera_view.transform.LookAt(Vector3.zero);
            camera_view.clearFlags=CameraClearFlags.SolidColor;camera_view.backgroundColor=new Color(.1f,.08f,.06f);
            fixture=new BattleSimulation(new[]{new Vector3(-2,0,0),new Vector3(2,0,0),new Vector3(20,0,20)},2,new bool[3],Array.Empty<Bounds>());
            fixture.selected[0]=true;
            overlay=new UnitFeedbackOverlay();models=new CharacterCrowdRenderer(3,VisualStyles.current.id);cursor=new RtsCursor();
            camera_view.orthographicSize=10;
            yield return null;yield return new WaitForEndOfFrame();
            require(overlay.health_bars==0&&overlay.ammunition_bars==0&&overlay.selection_rings==1,"full states hide bars but retain selection");
            fixture.health[0]=fixture.stats_for(0).health/2;
            fixture.ammunition[1]=fixture.stats_for(1).ammunition_capacity/2;
            yield return null;yield return new WaitForEndOfFrame();
            require(overlay.health_bars==1&&overlay.ammunition_bars==1,"health and ammunition visibility are independent");
            fixture.health[1]=fixture.stats_for(1).health/2;
            fixture.ammunition[0]=fixture.stats_for(0).ammunition_capacity/2;
            for(int i=0;i<8;i++)require(RtsCursor.direction_index(new Vector2(Mathf.Cos(i*Mathf.PI/4),Mathf.Sin(i*Mathf.PI/4)))==i+1,"eight cursor directions");
            foreach(float zoom in new[]{10f,50f,140f})
            {
                camera_view.orthographicSize=zoom;
                yield return new WaitForSecondsRealtime(.25f);yield return new WaitForEndOfFrame();
                require(overlay.health_bars==2&&overlay.ammunition_bars==2&&overlay.selection_rings==1,"damaged/depleted unselected human bars, selected ring, fog-safe enemies");
                var pixels=new Texture2D(Screen.width,Screen.height,TextureFormat.RGB24,false);
                pixels.ReadPixels(new Rect(0,0,Screen.width,Screen.height),0,0);pixels.Apply();
                int green=0,blue=0;
                foreach(var color in pixels.GetPixels32())
                {if(color.g>170&&color.r<110&&color.b<150)green++;if(color.b>210&&color.r<110&&color.g>90&&color.g<200)blue++;}
                Debug.Log($"[UnitUiSmoke] zoom={zoom} framebuffer={Screen.width}x{Screen.height} green={green} blue={blue}");
                if(green<=40||blue<=40)System.IO.File.WriteAllBytes("/tmp/zombie-unit-ui-readback.png",pixels.EncodeToPNG());
                require(green>40&&blue>40,"actual framebuffer contains green and blue UI at zoom "+zoom);
                Destroy(pixels);
                if(zoom==10)ScreenCapture.CaptureScreenshot("/tmp/zombie-unit-ui-regression.png");
            }
            fixture.selected[0]=false;fixture.health[1]=0; // Disposable death fixture; shared balance untouched.
            yield return null;yield return new WaitForEndOfFrame();
            require(overlay.health_bars==1&&overlay.ammunition_bars==1&&overlay.selection_rings==0,"dead units and deselection clear feedback");
            fixture.ammunition[0]=0;
            yield return null;yield return new WaitForEndOfFrame();
            require(overlay.ammunition_bars==1,"empty ammunition retains visible indicator");
            fixture.health[0]=fixture.stats_for(0).health;fixture.ammunition[0]=fixture.stats_for(0).ammunition_capacity;
            yield return null;yield return new WaitForEndOfFrame();
            require(overlay.health_bars==0&&overlay.ammunition_bars==0,"healed/resupplied bars disappear");
            check_friendly_vision();
            check_siege();
            Debug.Log("[UnitUiSmoke] PASS conditional green/blue at zoom10/50/140; empty ammo/heal/resupply; selection/death/fog; friendly building vision; eight cursor directions; 60s siege and pressure");
            Application.Quit(0);
        }
        private void Update()
        {
            if(overlay==null)return;
            models.begin_frame();
            for(int i=0;i<2;i++)if(fixture.health[i]>0)models.add(true,CharacterPose.Idle,Time.time,fixture.positions[i],Quaternion.identity);
            models.draw();overlay.draw(fixture,null,false,camera_view);cursor.update(Vector2.right,true,false);
        }
        private void check_siege()
        {
            var config=JsonUtility.FromJson<SiegeConfig>(Resources.Load<TextAsset>("SiegeConfig").text);
            var siege=new FrontierSiege(config);
            siege.step(59,fixture,null,Vector3.zero);require(siege.waves==0&&!fixture.activated[2],"no early siege");
            siege.step(1,fixture,null,Vector3.zero);require(siege.waves==1&&fixture.activated[2]&&fixture.siege_units==1,"exact 60s activates all survivors");
            siege.step(600,fixture,null,Vector3.zero);require(siege.waves==1,"test mode does not duplicate all-map wave");
            float interval=siege.interval;
            siege.record_expansion(new Vector3(120,0,120),Vector3.zero);float pressure=siege.pressure;
            require(pressure>0&&siege.interval<interval,"expansion shortens wave interval");
            siege.record_expansion(Vector3.zero,Vector3.zero);require(siege.pressure==pressure,"retreat does not erase pressure");
        }
        private void check_friendly_vision()
        {
            using(var fog=new ZombieGame.Vision.CombatFog(Resources.Load<Material>("FrontierFog")))
            {
                var building=fixture.add_building_target(new Bounds(new Vector3(70,20,70),Vector3.one*4),"VISION FIXTURE");
                var headquarters=fixture.add_building_target(new Bounds(new Vector3(-70,0,70),Vector3.one*4),"HQ VISION FIXTURE",true);
                fog.update_visibility(fixture);
                require(fog.is_visible(new Vector3(70,0,70))&&fog.is_visible(headquarters.bounds.center),"all building kinds supply ground-plane sight");
                float sight=ZombieGame.Balance.UnitBalance.config.human_sight;
                require(fog.is_visible(new Vector3(70+sight-1,0,70))&&!fog.is_visible(new Vector3(70+sight+1,0,70)),"building sight uses shared radius");
                require(fog.is_visible(fixture.positions[0])&&!fog.is_visible(fixture.positions[2]),"living friendly sight does not reveal distant enemies");
                building.health=0;headquarters.infected=true;
                fog.update_visibility(fixture);
                require(!fog.is_visible(building.bounds.center)&&fog.is_explored(building.bounds.center)&&!fog.is_visible(headquarters.bounds.center),"destroyed and infected buildings lose vision but retain exploration");
                fixture.buildings.Remove(building);fixture.buildings.Remove(headquarters);
            }
        }
        private static void require(bool condition,string message){if(!condition)throw new InvalidOperationException("UI regression: "+message);}
        private void OnDestroy(){cursor?.Dispose();overlay?.Dispose();fixture?.Dispose();Application.logMessageReceived-=fail_on_error;}
    }
}
