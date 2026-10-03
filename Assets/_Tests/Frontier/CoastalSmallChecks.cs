using System;
using System.Collections;
using System.IO;
using UnityEngine;
using ZombieGame.World;
using ZombieGame.CharacterTests;

namespace ZombieGame.FrontierTests
{
    /// <summary>Explicit disposable standalone fixture; accelerated pacing never runs in normal play.</summary>
    public sealed class CoastalSmallChecks:MonoBehaviour
    {
        private FrontierGame game;
        private bool camera_fixture;
        private void LateUpdate(){if(camera_fixture){Camera.main.orthographicSize=34;game.focus_camera(new Vector3(-91,0,-87));}}
        private IEnumerator Start()
        {
            if(Array.IndexOf(Environment.GetCommandLineArgs(),"-coastalSmallSmoke")<0)yield break;
            game=GetComponent<FrontierGame>();camera_fixture=true;
            yield return new WaitForSecondsRealtime(2);
            require(game.is_paused&&game.session.outcome==CoastalOutcome.Preparing,"starts paused in briefing");
            require(game.current.living_soldiers==120&&game.current.zombie_count==360,"small authored population");
            require(game.map.regions.TrueForAll(region=>region.kind!=LandscapeKind.Forest&&region.kind!=LandscapeKind.Cliff),"no procedural props or invisible forest blockers");
            require(game.production.config.recipes.Length==7,"only imported units and supported facilities offered");
            foreach(var recipe in game.production.config.recipes)if(recipe.is_unit)require(game.has_imported_unit(recipe.recruit_id),"authored recruit model "+recipe.recruit_id);
            require(!game.has_imported_unit("firearm_infantry"),"missing model cannot silently fall back");
            check_outcomes();
            var app_folder=new DirectoryInfo(Application.dataPath);
            while(app_folder!=null&&!app_folder.Name.EndsWith(".app"))app_folder=app_folder.Parent;
            if(app_folder==null)throw new InvalidOperationException("Small smoke requires standalone app");
            string folder=Path.Combine(app_folder.Parent.FullName,"ArtReview/CoastalSkirmish");Directory.CreateDirectory(folder);
            yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot(Path.Combine(folder,"briefing.png"));
            game.begin_session();require(game.can_control&&!game.is_paused,"start enables player controls");
            int recruit=Array.FindIndex(game.production.config.recipes,recipe=>recipe.recruit_id=="heavy_crossbowman");
            require(game.production.enqueue(recruit,game.current.reserve_soldiers),"recruit queue accepts supported archer");
            game.production.step(game.production.config.recipes[recruit].seconds+.1f,game.finish_production);
            require(game.current.living_soldiers==121,"real recruitment deploys original model");
            int depot=Array.FindIndex(game.production.config.recipes,recipe=>recipe.id=="depot");
            require(game.construction.try_place(depot,new Vector3(-90,0,-88)),"place imported depot");
            game.production.step(game.production.config.recipes[depot].seconds+.1f,game.finish_production);
            require(game.construction.facilities[0].complete&&game.supply.depots.Count==2,"construction finishes and supplies ammunition");
            // Pacing fixture only; no unit HP, movement, contact or attack overrides.
            game.siege.step(game.session_config.preparation_seconds,current_battle(),game.construction,game.map.base_center);
            require(game.siege.waves==1&&game.siege.last_wave_units==80,"first wave exact cohort");
            game.siege.step(game.session_config.wave_interval_seconds,current_battle(),game.construction,game.map.base_center);
            require(game.siege.waves==2&&game.siege.last_wave_units==120,"second wave exact cohort");
            game.siege.step(game.session_config.wave_interval_seconds,current_battle(),game.construction,game.map.base_center);
            require(game.siege.waves==3&&game.siege.last_wave_units==160&&game.siege.all_waves_sent,"final cohort committed to HQ");
            game.siege.step(999,current_battle(),game.construction,game.map.base_center);
            require(game.siege.waves==3,"finite game never adds a fourth wave");
            float started=Time.realtimeSinceStartup;int frames=0;
            while(Time.realtimeSinceStartup-started<50){frames++;yield return null;}
            require(game.current.hits>0&&game.current.dead_zombies>0,"real defensive battle damages and kills enemies");
            require(game.current.geometry_errors==0,"no terrain intrusion");
            require(CrowdSpacingChecks.minimum_ratio(game.current,true)>=.999f,"approved zombie spacing preserved");
            require(ImportedArchitecture.maximum_aspect_error<.001f,"source proportions preserved");
            yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot(Path.Combine(folder,"battle.png"));
            Debug.Log($"[CoastalSmall] PASS start, cohort pacing, original art, recruitment, construction, outcomes; framebuffer={Screen.width}x{Screen.height} fps={frames/(Time.realtimeSinceStartup-started):F1} kills={game.current.dead_zombies} humans={game.current.living_soldiers} geometry={game.current.geometry_errors}");
            camera_fixture=false;yield return new WaitForSecondsRealtime(1);Application.Quit(0);
        }
        private ZombieGame.Combat.BattleSimulation current_battle()=>game.current;
        private static void check_outcomes()
        {
            var session=new CoastalSession();session.begin();
            session.evaluate(true,false,0,false);require(!session.finished,"no early victory before final wave");
            session.evaluate(true,true,0,true);require(!session.finished,"pending infection prevents premature victory");
            session.evaluate(true,true,0,false);require(session.outcome==CoastalOutcome.Won,"living HQ and cleared cohorts win");
            var lost=new CoastalSession();lost.begin();lost.evaluate(false,true,0,false);
            require(lost.outcome==CoastalOutcome.Lost,"HQ loss takes precedence");
            lost.begin();require(lost.outcome==CoastalOutcome.Lost,"begin cannot resume a finished round");
        }
        private static void require(bool condition,string message)
        {if(!condition){Debug.LogError("[CoastalSmall] FAIL "+message);Application.Quit(1);throw new InvalidOperationException(message);}}
    }
}
