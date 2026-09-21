using System;
using System.Collections;
using UnityEngine;
using ZombieGame.World;

namespace ZombieGame.FrontierTests
{
    /// <summary>Full-map disposable run. Advances only the scenario clock, never unit speed or health.</summary>
    public sealed class DefenseSmoke:MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void install_fixture()
        {
            if(Array.IndexOf(Environment.GetCommandLineArgs(),"-defenseSmoke")<0)return;
            new GameObject("Full-map defense regression").AddComponent<DefenseSmoke>();
            Application.logMessageReceived+=fail_on_error;
        }
        private static void fail_on_error(string message,string trace,LogType type)
        {if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)Application.Quit(1);}
        private IEnumerator Start()
        {
            var game=FindFirstObjectByType<FrontierGame>();
            while(game.current==null)yield return null;
            game.focus_camera(new Vector3(-99,0,-92));
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot("/tmp/zombie-defense-ui.png");
            yield return null;
            game.siege.step(game.siege.remaining,game.current,game.construction,game.map.base_center);
            require(game.siege.waves==1&&game.current.siege_units==game.current.zombie_count-game.current.dead_zombies,"all surviving map zombies ordered once");
            float finish=Time.time+12;int peak_moving=0;
            while(Time.time<finish)
            {peak_moving=Mathf.Max(peak_moving,game.current.moving_now);yield return null;}
            Debug.Log($"[DefenseSmoke] navigation moving_peak={peak_moving} pending={game.current.pending} path_failures={game.current.path_failures} active={game.current.active_now}");
            require(peak_moving>1000,"native navigation moves the mass assault");
            require(game.current.geometry_errors==0,"no geometry intrusion");
            require(game.can_control,"player controls retained");
            Debug.Log($"[DefenseSmoke] PASS ordered={game.current.siege_units} moving_peak={peak_moving} geometry=0 human_controls=true");
            Application.Quit(0);
        }
        private static void require(bool condition,string message){if(!condition)throw new InvalidOperationException("Defense regression: "+message);}
        private void OnDestroy(){Application.logMessageReceived-=fail_on_error;}
    }
}
