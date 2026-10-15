using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.AI;
using Unity.Profiling;
using ZombieGame.Combat;
using ZombieGame.Controls;
using ZombieGame.Presentation;
using ZombieGame.World;

namespace ZombieGame.Tests.V010Defense
{
    /// <summary>Thin disposable integration fixture. It accelerates only scenario wave scheduling, never unit balance or contact behavior.</summary>
    public sealed class V010DefenseChecks : MonoBehaviour
    {
        [Serializable] private sealed class Report
        {
            public string version = "V0.10 preview 1 integration smoke",status = "RUNNING",failure;
            public string balance_source = "balance/unit_balance.json / UnitBalance";
            public string contact_baseline = "6325116; zombie-zombie and zombie-human only";
            public string pacing_fixture = "Only siege scheduling advanced by 60 seconds; unit time, health, damage and navigation unchanged.";
            public List<string> passed = new List<string>();
            public int humans,zombies,trees,grass_batches,shots,hits,kills,bites,geometry_errors;
            public float minimum_contact_ratio,elapsed_battle_seconds,mean_frame_ms,p95_frame_ms;
            public string device,framebuffer;
            public int active_frame_samples;
            public float active_mean_frame_ms,active_p95_frame_ms;
            public bool full_scenario;
            public string outcome;
            public int waves;
            public long peak_batches = -1,peak_triangles = -1;
            public double mean_simulation_ms,mean_presentation_ms;
            public string crop_shadow_mode,player_order_fixture = "None; defenders hold unless commanded.";
            public float terrain_pixel_contribution;
            public List<string> authored_animation_samples = new List<string>();
        }
        public bool run_on_start;
        private FrontierGame game;
        private Report report;
        private string folder;
        private bool camera_fixture;
        private Vector3 fixture_focus;
        private float fixture_zoom = 25;
        private ProfilerRecorder batch_counter,triangle_counter;

        private void LateUpdate()
        {
            if(!camera_fixture || !game)return;
            Camera.main.orthographicSize = fixture_zoom;game.focus_camera(fixture_focus);
        }

        private IEnumerator Start()
        {
            if(!run_on_start && Array.IndexOf(Environment.GetCommandLineArgs(),"-v010Check") < 0)yield break;
            game = GetComponent<FrontierGame>();report = new Report();folder = find_report_folder();Directory.CreateDirectory(folder);
            report.full_scenario = Array.IndexOf(Environment.GetCommandLineArgs(),"-v010FullCheck") >= 0;
            fixture_focus = game.defense_scenario.initial_camera_focus;camera_fixture = true;
            batch_counter = ProfilerRecorder.StartNew(ProfilerCategory.Render,"Batches Count");
            triangle_counter = ProfilerRecorder.StartNew(ProfilerCategory.Render,"Triangles Count");
            yield return new WaitForSecondsRealtime(1);
            try{check_initial_state();check_paused_minimap();check_terrain_pixels();check_authored_frames();save_report();}
            catch(Exception exception){fail(exception);yield break;}
            bool force_crop_shadows = Array.IndexOf(Environment.GetCommandLineArgs(),"-v010CropShadows") >= 0;
            var environment = FindFirstObjectByType<FrozenEnvironmentView>();
            foreach(var renderer in environment.GetComponentsInChildren<MeshRenderer>())
                if(renderer.name.StartsWith("Pasture",StringComparison.Ordinal) && renderer.name.IndexOf("_crop",StringComparison.Ordinal) >= 0)
                {
                    if(force_crop_shadows)renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                    report.crop_shadow_mode = renderer.shadowCastingMode.ToString();
                }
            yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot(Path.Combine(folder,"briefing.png"));
            game.begin_session();yield return null;yield return null;
            fixture_zoom = 10;fixture_focus = game.current.positions[8];
            yield return null;yield return null;yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(Path.Combine(folder,"defenders-detail.png"));
            fixture_zoom = 25;fixture_focus = game.defense_scenario.initial_camera_focus;
            yield return null;yield return null;
            try
            {
                check_navigation();
                ZombieGame.FrontierTests.CoastalControlChecks.check_live_orders(game);
                pass("Existing production box selection, right move, A attack, groups, double-F2 and HQ control handlers");
                game.siege.step(game.session_config.preparation_seconds,game.current,game.construction,game.map.base_center);
                require(game.siege.waves == 1 && game.siege.last_wave_units == 80,"First 80-unit cohort uses persistent production HQ siege orders");
            }
            catch(Exception exception){fail(exception);yield break;}
            var samples = new List<float>();var active_samples = new List<float>();
            float started = Time.realtimeSinceStartup,last_action = float.NegativeInfinity;
            int previous_actions = game.current.shots+game.current.hits+game.current.bites;
            double simulation_total = 0,presentation_total = 0;
            bool combat_captured = false;
            float window_seconds = report.full_scenario ? 300 : 50;
            bool cleanup_requested = Array.IndexOf(Environment.GetCommandLineArgs(),"-v010Cleanup") >= 0;
            int cleanup_step = 0;
            while(Time.realtimeSinceStartup-started < window_seconds && !game.session.finished)
            {
                float frame_ms = Time.unscaledDeltaTime*1000;samples.Add(frame_ms);
                simulation_total += game.last_simulation_ms;presentation_total += game.last_presentation_ms;
                if(batch_counter.Valid)report.peak_batches = Math.Max(report.peak_batches,batch_counter.LastValue);
                if(triangle_counter.Valid)report.peak_triangles = Math.Max(report.peak_triangles,triangle_counter.LastValue);
                int actions = game.current.shots+game.current.hits+game.current.bites;
                if(cleanup_requested && cleanup_step < 3 && game.siege.all_waves_sent && Time.realtimeSinceStartup-started > 180+cleanup_step*40)
                {
                    var targets = new[]{new Vector3(0,0,24),new Vector3(-15,0,18),new Vector3(1,0,29)};
                    game.controls.select_all();game.controls.issue_selected(SoldierOrder.AttackMove,targets[cleanup_step]);cleanup_step++;
                    report.player_order_fixture = "Scripted player cleanup after all3 waves: production select-all/A-move at180s(0,24),220s(-15,18),260s(1,29), stopping when session finishes. No enemy, health, path or contact overrides. Auto-hold stall remains a known failure.";
                }
                if(actions != previous_actions)last_action = Time.realtimeSinceStartup;
                if(Time.realtimeSinceStartup-last_action <= .75f)active_samples.Add(frame_ms);
                if(!combat_captured && game.current.hits > 0)
                {ScreenCapture.CaptureScreenshot(Path.Combine(folder,"battle-active.png"));combat_captured = true;}
                previous_actions = actions;yield return null;
            }
            try
            {
                report.shots = game.current.shots;report.hits = game.current.hits;report.kills = game.current.dead_zombies;report.bites = game.current.bites;
                report.geometry_errors = game.current.geometry_errors;
                report.minimum_contact_ratio = ZombieGame.CharacterTests.CrowdSpacingChecks.minimum_ratio(game.current,true);
                report.elapsed_battle_seconds = Time.realtimeSinceStartup-started;
                samples.Sort();float total = 0;foreach(float sample in samples)total += sample;
                report.mean_frame_ms = samples.Count == 0 ? 0 : total/samples.Count;
                report.p95_frame_ms = samples.Count == 0 ? 0 : samples[Mathf.Min(samples.Count-1,Mathf.FloorToInt(samples.Count*.95f))];
                active_samples.Sort();float active_total = 0;foreach(float sample in active_samples)active_total += sample;
                report.active_frame_samples = active_samples.Count;
                report.active_mean_frame_ms = active_samples.Count == 0 ? 0 : active_total/active_samples.Count;
                report.active_p95_frame_ms = active_samples.Count == 0 ? 0 : active_samples[Mathf.Min(active_samples.Count-1,Mathf.FloorToInt(active_samples.Count*.95f))];
                report.device = SystemInfo.deviceModel+" / "+SystemInfo.graphicsDeviceName;
                report.framebuffer = Screen.width+"x"+Screen.height+"; ortho="+Camera.main.orthographicSize+"; editor="+Application.isEditor;
                report.outcome = game.session.outcome.ToString();report.waves = game.siege.waves;
                report.mean_simulation_ms = samples.Count == 0 ? 0 : simulation_total/samples.Count;
                report.mean_presentation_ms = samples.Count == 0 ? 0 : presentation_total/samples.Count;
                require(report.shots > 0 && report.hits > 0 && report.kills > 0,"Real production battle shoots, damages and kills enemies");
                require(report.geometry_errors == 0,"No production navigation intrusion");
                require(report.minimum_contact_ratio >= .999f,"Frozen zombie contact separation remains intact");
                require(game.current.positions.Length == game.current.total_count,"Original production renderer and simulation capacity agree");
                if(report.full_scenario)require(game.session.finished,"Full scenario reaches a genuine production win/loss outcome within 300 real seconds");
                report.status = "PASS";save_report();
            }
            catch(Exception exception){fail(exception);yield break;}
            Camera.main.orthographicSize = 25;game.focus_camera(game.defense_scenario.initial_camera_focus);
            yield return null;yield return null;
            yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot(Path.Combine(folder,"battle.png"));
            camera_fixture = false;
            dispose_counters();
            Debug.Log("[V0.10] PASS production integration, not art acceptance; report="+folder);
            if(Array.IndexOf(Environment.GetCommandLineArgs(),"-v010CheckExit") >= 0){yield return new WaitForSecondsRealtime(1);Application.Quit(0);}
        }

        private void check_initial_state()
        {
            require(game && game.defense_scenario && game.current != null,"Opt-in production FrontierGame scenario initialized");
            require(game.is_paused && game.session.outcome == CoastalOutcome.Preparing,"Starts paused in existing briefing");
            report.humans = game.current.living_soldiers;report.zombies = game.current.zombie_count;
            require(report.humans == 64 && report.zombies == 240,"Scenario population is 64 defenders and 240 zombies");
            require(game.playable_bounds == new Rect(-64,-64,128,128),"Camera and minimap explicitly use the 128m map");
            var frozen = FindFirstObjectByType<FrozenEnvironmentView>();
            require(frozen && frozen.transform.position == new Vector3(0,-4.2f,0),"Whole frozen environment receives only Y=-4.2 translation");
            var terrain = frozen.GetComponentInChildren<Terrain>();
            require(terrain && terrain.terrainData && terrain.materialTemplate && terrain.terrainData.terrainLayers.Length == 6,
                "Frozen scene retains actual Terrain and all six PBR layer slots");
            foreach(var layer in terrain.terrainData.terrainLayers)
                require(layer && layer.diffuseTexture && layer.normalMapTexture,"Frozen Terrain layer and PBR textures survive asset serialization");
            report.trees = frozen.frozen_tree_count;report.grass_batches = frozen.frozen_grass_count;
            require(report.trees == 81,"All 81 frozen tree placements retained");
            require(!FindFirstObjectByType<ZombieGame.Tests.EnvironmentShowcase.EnvironmentShowcase>(),"No review controller competes with formal gameplay");
            require(FindObjectsByType<FrontierGame>(FindObjectsSortMode.None).Length == 1,"Exactly one battle controller");
            foreach(var region in game.defense_scenario.regions)
                if(region.kind == LandscapeKind.Building)
                    require(region.label == "COMMAND HALL" || region.label == "ARROW WORKS" || region.label.StartsWith("STONE WALL ",StringComparison.Ordinal),"Combat targets exclude fields, ore and neutral ruins");
            require(game.structures.headquarters != null && game.structures.headquarters.health > 0,"Authored headquarters supplies objective and shared sight");
            int workshops = 0;
            foreach(var building in game.current.buildings)if(building.health > 0 && building.label == "ARROW WORKS")workshops++;
            require(game.economy.arrow_workshops == workshops,"Scenario ammunition production matches its living authored workshops");
            foreach(var recipe in game.production.config.recipes)require(recipe.is_unit && game.has_imported_unit(recipe.recruit_id),"Recruitment never invents missing unit or construction art");
            for(int index = 0; index < game.current.total_count; index++)
                if(game.current.health[index] > 0 && !game.current.is_reserve(index))require(game.has_imported_unit(game.current.stats_for(index).id),"All living deployed roles have approved imported art");
        }

        private void check_paused_minimap()
        {
            var controls = game.controls;var saved_focus = game.camera_focus;var area = controls.minimap_bounds;
            try
            {
                controls.handle_mouse(new Event {type = EventType.MouseDown,button = 0,mousePosition = area.center});
                controls.handle_mouse(new Event {type = EventType.MouseDrag,button = 0,mousePosition = area.center+Vector2.right*area.width*.1f});
                require(controls.minimap_dragging && Mathf.Abs(game.camera_focus.x-12.8f) < .01f,"Paused 128m minimap drag maps to correct world coordinates");
                controls.handle_mouse(new Event {type = EventType.MouseDrag,button = 0,mousePosition = area.position-Vector2.one*100});
                require(Mathf.Abs(game.camera_focus.x+64) < .01f && Mathf.Abs(game.camera_focus.z-64) < .01f,"Minimap capture clamps outside its rectangle");
                controls.handle_mouse(new Event {type = EventType.MouseUp,button = 0,mousePosition = area.center});
                require(!controls.minimap_dragging && !game.can_control,"Briefing drag releases without issuing unit commands");
            }
            finally{controls.cancel_pointer_capture();game.focus_camera(saved_focus);}
        }

        private void check_terrain_pixels()
        {
            var terrain = FindFirstObjectByType<FrozenEnvironmentView>().GetComponentInChildren<Terrain>();
            var camera = Camera.main;
            var old_target = camera.targetTexture;var old_active = RenderTexture.active;
            bool old_enabled = terrain.enabled;
            var target = RenderTexture.GetTemporary(Screen.width,Screen.height,24,RenderTextureFormat.ARGB32);
            Texture2D shown = null,hidden = null;
            try
            {
                camera.targetTexture = target;terrain.enabled = true;
                shown = capture_camera_pixels(camera,target);
                terrain.enabled = false;hidden = capture_camera_pixels(camera,target);
                var on_pixels = shown.GetPixels32();var off_pixels = hidden.GetPixels32();int changed = 0;
                for(int index = 0; index < on_pixels.Length; index++)
                {
                    var a = on_pixels[index];var b = off_pixels[index];
                    if(Mathf.Abs(a.r-b.r)+Mathf.Abs(a.g-b.g)+Mathf.Abs(a.b-b.b) > 12)changed++;
                }
                report.terrain_pixel_contribution = changed/(float)on_pixels.Length;
                File.WriteAllBytes(Path.Combine(folder,"terrain-on.png"),shown.EncodeToPNG());
                File.WriteAllBytes(Path.Combine(folder,"terrain-off.png"),hidden.EncodeToPNG());
                require(report.terrain_pixel_contribution > .0003f,"Packaged Terrain actually contributes pixels in a same-frame on/off comparison");
            }
            finally
            {
                terrain.enabled = old_enabled;camera.targetTexture = old_target;RenderTexture.active = old_active;
                RenderTexture.ReleaseTemporary(target);if(shown)Destroy(shown);if(hidden)Destroy(hidden);
            }
        }

        private static Texture2D capture_camera_pixels(Camera camera,RenderTexture target)
        {
            camera.Render();RenderTexture.active = target;
            var pixels = new Texture2D(target.width,target.height,TextureFormat.RGBA32,false);
            pixels.ReadPixels(new Rect(0,0,target.width,target.height),0,0);pixels.Apply();return pixels;
        }

        private void check_authored_frames()
        {
            foreach(var entry in game.imported_roster.units)
            {
                if(entry.unit_id != "archer" && entry.unit_id != "walker" && entry.unit_id != "exploder" && entry.unit_id != "zombie_hound")continue;
                foreach(var pose in new[]{CharacterPose.Run,CharacterPose.Attack})
                {
                    var clip = entry.frames.poses[(int)pose];
                    require(!string.IsNullOrEmpty(clip.source_clip) && clip.frames.Length > 1,"Deployed walking/attack slots retain authored multi-frame clips");
                    var first = clip.frames[0].vertices;float maximum_delta = 0;
                    for(int frame = 1; frame < clip.frames.Length; frame++)
                    {
                        var vertices = clip.frames[frame].vertices;
                        require(vertices.Length == first.Length,"Authored frame topology remains consistent");
                        for(int vertex = 0; vertex < first.Length; vertex += 32)
                            maximum_delta = Mathf.Max(maximum_delta,(vertices[vertex]-first[vertex]).magnitude);
                    }
                    report.authored_animation_samples.Add(entry.unit_id+"/"+pose+": "+clip.source_clip+"; frames="+clip.frames.Length+"; maximum sampled vertex delta="+maximum_delta.ToString("F6"));
                    require(maximum_delta > .00001f,"Deployed walking/attack clips contain actual articulated mesh motion");
                }
            }
        }

        private void check_navigation()
        {
            require(game.current.crowd.walls.Length == game.map.regions.Count,"Each scenario obstacle has exactly one native carver/wall record");
            foreach(var point in new[]{new Vector3(-68,0,0),new Vector3(68,0,0),new Vector3(0,0,-68),new Vector3(0,0,68)})
                require(!NavMesh.SamplePosition(point,out _,.1f,NavMesh.AllAreas),"Scenario boundary blocks walking into the 256m backing plane");
            var portal = new Vector3(4,0,-3.456815f);
            require(NavMesh.SamplePosition(portal,out _,.2f,NavMesh.AllAreas),"Authored open north portal remains traversable; no gate leaf invented");
            for(int index = 0; index < game.current.soldier_count; index++)
                if(game.current.health[index] > 0)require(game.current.crowd.agents[index].isOnNavMesh,"Each deployed defender remains on native NavMesh after source carving");
            for(int index = game.current.soldier_count; index < game.current.soldier_count+game.defense_scenario.zombies.Length; index++)
            {
                var agent = game.current.crowd.agents[index];
                var filter = new NavMeshQueryFilter {agentTypeID = agent.agentTypeID,areaMask = agent.areaMask};
                // Unalerted zombies intentionally have disabled agents. They must have a
                // valid landing surface, not falsely be reported as already bound agents.
                require(NavMesh.SamplePosition(game.current.positions[index],out _,.2f,filter) &&
                    (!agent.enabled || agent.isOnNavMesh),"Every initial zombie has its own valid native surface after carving");
            }
            var path = new NavMeshPath();
            int routed = 0;
            for(int index = game.current.soldier_count; index < game.current.soldier_count+game.defense_scenario.zombies.Length; index++)
                if(NavMesh.CalculatePath(game.current.positions[index],portal,NavMesh.AllAreas,path) && path.status == NavMeshPathStatus.PathComplete)routed++;
            require(routed == game.defense_scenario.zombies.Length,"Every initial zombie has a native route to the north portal");
        }

        private void require(bool condition,string label)
        {if(!condition)throw new InvalidOperationException(label);pass(label);}
        private void pass(string label){if(!report.passed.Contains(label))report.passed.Add(label);}
        private void save_report(){File.WriteAllText(Path.Combine(folder,"integration-report.json"),JsonUtility.ToJson(report,true));}
        private void fail(Exception exception)
        {
            camera_fixture = false;
            dispose_counters();
            report.status = "FAIL";report.failure = exception.ToString();save_report();Debug.LogError("[V0.10] FAIL "+exception);
            if(Array.IndexOf(Environment.GetCommandLineArgs(),"-v010CheckExit") >= 0)Application.Quit(1);
        }
        private void OnDestroy(){dispose_counters();}
        private void dispose_counters()
        {if(batch_counter.Valid)batch_counter.Dispose();if(triangle_counter.Valid)triangle_counter.Dispose();}
        private static string find_report_folder()
        {
            if(Application.isEditor)return Path.GetFullPath("Builds/ArtReview/V010Defense");
            var app = new DirectoryInfo(Application.dataPath);
            while(app != null && !app.Name.EndsWith(".app",StringComparison.OrdinalIgnoreCase))app = app.Parent;
            if(app?.Parent == null)throw new InvalidOperationException("Cannot resolve V0.10 player report folder.");
            return Path.Combine(app.Parent.FullName,"ArtReview/V010Defense");
        }
    }
}
