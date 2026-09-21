using System;
using System.Collections;
using UnityEngine;
using ZombieGame.Presentation;
using ZombieGame.World;

namespace ZombieGame.FrontierTests
{
    public sealed class BattleFeedbackSmoke:MonoBehaviour
    {
        private BattleAudio audio_fixture;
        private MusketEffects smoke_fixture;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void install_fixture()
        {
            if(Array.IndexOf(Environment.GetCommandLineArgs(),"-feedbackSmoke")<0)return;
            var game=FindFirstObjectByType<FrontierGame>();if(game!=null)game.enabled=false;
            new GameObject("Disposable battle feedback fixture").AddComponent<BattleFeedbackSmoke>();
            Application.logMessageReceived+=fail_on_error;
        }
        private static void fail_on_error(string message,string trace,LogType type)
        {if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)Application.Quit(1);}
        private IEnumerator Start()
        {
            Application.runInBackground=true;
            audio_fixture=new BattleAudio(1,transform);smoke_fixture=new MusketEffects(transform);
            for(int i=0;i<400;i++){audio_fixture.play(0,1,0);smoke_fixture.fire(Vector3.zero,Vector3.forward);}
            for(int i=0;i<100;i++){audio_fixture.play(1,1,-.4f);audio_fixture.play(2,1,.4f);}
            require(audio_fixture.played_shots==8&&audio_fixture.played_stabs==4&&audio_fixture.played_growls==4,"per-category voice caps");
            require(audio_fixture.active_voices<=BattleAudio.VOICE_LIMIT,"global voice cap");
            require(smoke_fixture.live_particles>0,"smoke emitted");
            yield return new WaitForSeconds(2);
            require(smoke_fixture.live_particles>0,"smoke persists after two seconds");
            yield return new WaitForSeconds(1.5f);
            require(smoke_fixture.live_particles==0,"smoke expires after three seconds");
            smoke_fixture.clear();require(smoke_fixture.shot_count==0,"smoke reset");
            for(int i=0;i<3000;i++)smoke_fixture.fire(Vector3.zero,Vector3.forward);
            require(smoke_fixture.live_particles<=10500,"particle pool cap");
            Debug.Log("[BattleFeedbackSmoke] PASS clips load; 400 guns bounded to 8 voices; 4 stabs + 4 growls; smoke persists 2s/expires 3.5s; particle cap 10500");
            Application.Quit(0);
        }
        private static void require(bool condition,string description)
        {if(!condition)throw new InvalidOperationException("Battle feedback regression: "+description);}
        private void OnDestroy(){audio_fixture?.Dispose();smoke_fixture?.Dispose();Application.logMessageReceived-=fail_on_error;}
    }
}
