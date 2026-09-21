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
            for(int i=0;i<8;i++)require(RtsCursor.direction_index(new Vector2(Mathf.Cos(i*Mathf.PI/4),Mathf.Sin(i*Mathf.PI/4)))==i+1,"eight cursor directions");
            foreach(float zoom in new[]{10f,50f,140f})
            {
                camera_view.orthographicSize=zoom;
                yield return new WaitForSecondsRealtime(.25f);yield return new WaitForEndOfFrame();
                require(overlay.health_bars==2&&overlay.ammunition_bars==2&&overlay.selection_rings==1,"full-health/full-ammo unselected human bars, selected ring, fog-safe enemies");
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
            check_siege();
            Debug.Log("[UnitUiSmoke] PASS rendered green/blue at zoom10/50/140; persistent bars; selection/death/fog; eight cursor directions; 60s siege and pressure");
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
        private static void require(bool condition,string message){if(!condition)throw new InvalidOperationException("UI regression: "+message);}
        private void OnDestroy(){cursor?.Dispose();overlay?.Dispose();fixture?.Dispose();Application.logMessageReceived-=fail_on_error;}
    }
}
