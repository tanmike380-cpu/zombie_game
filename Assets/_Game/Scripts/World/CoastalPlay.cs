using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ZombieGame.World
{
    public sealed partial class FrontierGame
    {
        public bool coastal_skirmish;
        public CoastalSession session {get;private set;}
        public CoastalSessionConfig session_config {get;private set;}
        public bool session_overlay=>session!=null&&(session.outcome==CoastalOutcome.Preparing||session.finished);
        public void begin_session()
        {
            if(session?.outcome!=CoastalOutcome.Preparing)return;
            session.begin();paused=false;Time.timeScale=1;
        }
        public void restart_session()
        {
            if(!coastal_skirmish)return;
            Time.timeScale=1;SceneManager.LoadScene(gameObject.scene.name);
        }
        private void evaluate_session()
        {
            if(session==null)return;
            bool pending=false;foreach(var building in current.buildings)if(building.infection_remaining>0)pending=true;
            session.evaluate(!defeated,siege.all_waves_sent,current.zombie_count-current.dead_zombies,pending);
            if(session.finished){paused=true;Time.timeScale=0;construction.cancel_preview();}
        }
        private void configure_session_siege(SiegeConfig config)
        {
            config.defense_test=false;config.campaign_first_wave_seconds=session_config.preparation_seconds;
            config.low_pressure_interval_seconds=config.high_pressure_interval_seconds=session_config.wave_interval_seconds;
            config.finite_wave_counts=session_config.wave_counts;
        }
        public bool has_imported_unit(string id)
        {
            if(id=="greek_fire")return Resources.Load<GameObject>("CoastalBuildings/GreekFire")!=null;
            return imported_roster!=null&&Array.Exists(imported_roster.units,entry=>entry.unit_id==id&&entry.frames!=null);
        }
        private void filter_imported_recipes(HeadquartersConfig config)
        {
            // Missing authored art is hidden, never substituted with a procedural soldier or engine.
            config.recipes=Array.FindAll(config.recipes,recipe=>recipe.is_unit?has_imported_unit(recipe.recruit_id):
                recipe.id=="food"||recipe.id=="arrows"||recipe.id=="powder"||recipe.id=="depot");
        }
        private void validate_imported_units()
        {
            for(int i=0;i<current.total_count;i++)
                if(current.health[i]>0&&!current.is_reserve(i)&&!has_imported_unit(current.stats_for(i).id))
                    throw new InvalidOperationException("No authored Blender model for deployed unit: "+current.stats_for(i).id);
        }
    }
}
