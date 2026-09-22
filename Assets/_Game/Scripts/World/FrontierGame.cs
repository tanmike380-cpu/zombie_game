using UnityEngine;
using ZombieGame.Combat;
using ZombieGame.Controls;
using ZombieGame.Vision;
using ZombieGame.Presentation;

namespace ZombieGame.World
{
    /// <summary>Playable world slice; no dependency on benchmark or test assemblies.</summary>
    public sealed class FrontierGame : MonoBehaviour, IRtsBattleView
    {
        public Material fog_template;
        public Shader landscape_shader;
        public BattleSimulation current {get;private set;}
        public CombatFog current_fog {get;private set;}
        public bool can_control => current!=null && !paused && !defeated;
        public FrontierStructures structures {get;private set;}
        public bool defeated => structures?.headquarters?.infected??false;
        public bool reveal_map {get;private set;}
        public Vector3 camera_focus {get;private set;}
        public FrontierMap map {get;private set;}
        public FrontierEconomy economy {get;private set;}
        public HeadquartersProduction production {get;private set;}
        public FrontierConstruction construction {get;private set;}
        public AmmunitionSupply supply {get;private set;}
        public bool headquarters_selected {get;private set;}
        private FrontierLandscape landscape;
        private FrontierBattleView battle_view;
        private BattleAudio battle_audio;
        private RtsCursor game_cursor;
        private Vector2 gui_pointer;
        private bool has_gui_pointer;
        public FrontierSiege siege {get;private set;}
        private int previous_navigation_iterations=-1;
        private RtsBattleInput input;
        private FrontierHud hud;
        public RtsBattleInput controls => input;
        public bool is_paused => paused;
        private float next_fog;
        private bool paused;
        public bool pointer_over_ui(Vector2 point) => FrontierHud.contains(point,hud!=null&&hud.show_roster);
        public Color32 obstacle_color(int index)
        {
            switch(map.regions[index].kind)
            {
                case LandscapeKind.River:return new Color32(35,105,135,255);
                case LandscapeKind.Forest:return new Color32(20,64,23,255);
                case LandscapeKind.Cliff:return new Color32(105,104,88,255);
                default:return new Color32(150,125,75,255);
            }
        }

        private void Start()
        {
            Application.runInBackground=true;QualitySettings.vSyncCount=1;Application.targetFrameRate=60;
            Camera.main.allowDynamicResolution=false;Camera.main.allowMSAA=true;QualitySettings.antiAliasing=4;
            Camera.main.clearFlags=CameraClearFlags.SolidColor;
            var arguments=System.Environment.GetCommandLineArgs();
            int style_argument=System.Array.IndexOf(arguments,"-artStyle");
            if(style_argument>=0&&style_argument+1<arguments.Length&&int.TryParse(arguments[style_argument+1],out int requested_style))VisualStyles.select(requested_style);
            map=new FrontierMap();
            landscape=new FrontierLandscape(map,transform,landscape_shader);
            current=new BattleSimulation(map.spawns,FrontierMap.HUMAN_CAPACITY,map.explosive,map.blockers,initial_humans:FrontierMap.SOLDIERS,unit_ids:map.unit_ids,infection_reserve:1024);
            economy=new FrontierEconomy(JsonUtility.FromJson<FrontierEconomyConfig>(Resources.Load<TextAsset>("FrontierEconomy").text));
            production=new HeadquartersProduction(economy,JsonUtility.FromJson<HeadquartersConfig>(Resources.Load<TextAsset>("HeadquartersProduction").text));
            supply=new AmmunitionSupply(economy);supply.depots.Add(map.initial_depot);
            current_fog=new CombatFog(fog_template,.08f,true);
            current_fog.explore_area(new Rect(-124,-124,76,76));
            current_fog.update_visibility(current);current.player_visibility=current_fog.is_visible;
            battle_view=new FrontierBattleView(current.total_count,transform,landscape_shader);
            battle_audio=new BattleAudio(current.total_count,transform);
            game_cursor=new RtsCursor();
            input=gameObject.AddComponent<RtsBattleInput>();input.game=this;input.custom_command_panel=true;input.select_all();
            input.select_structure=try_select_headquarters;
            input.selection_changed=()=>{headquarters_selected=false;construction?.cancel_preview();};
            construction=new FrontierConstruction(this,landscape,landscape_shader);
            structures=new FrontierStructures(this,landscape,landscape_shader);
            current_fog.update_visibility(current);
            siege=new FrontierSiege(JsonUtility.FromJson<SiegeConfig>(Resources.Load<TextAsset>("SiegeConfig").text));
            previous_navigation_iterations=UnityEngine.AI.NavMesh.pathfindingIterationsPerFrame;
            UnityEngine.AI.NavMesh.pathfindingIterationsPerFrame=siege.config.navigation_iterations_per_frame;
            input.intercept_world_input=construction.handle_input;
            hud=new FrontierHud(this);
            configure_lighting();
            Camera.main.orthographicSize=24;focus_camera(new Vector3(-99,0,-92));
            Debug.Log($"[Frontier] READY map=256x256 soldiers={current.living_soldiers} reserve={current.reserve_soldiers} zombies={current.zombie_count} blockers={map.blockers.Length} framebuffer={Screen.width}x{Screen.height}; eight zombie roles; new role balance provisional");
        }
        public void focus_camera(Vector3 point)
        {
            camera_focus=new Vector3(Mathf.Clamp(point.x,-128,128),0,Mathf.Clamp(point.z,-128,128));
            Camera.main.transform.rotation=Quaternion.Euler(VisualStyles.current.camera_pitch,VisualStyles.current.camera_yaw,0);
            Camera.main.transform.position=camera_focus-Camera.main.transform.forward*180;
        }
        private void Update()
        {
            if(current==null)return;
            if(Input.GetKeyDown(KeyCode.F5))switch_visual_style(0);
            if(Input.GetKeyDown(KeyCode.F6))switch_visual_style(1);
            if(Input.GetKeyDown(KeyCode.F7))switch_visual_style(2);
            if(Input.GetKeyDown(KeyCode.Space)) { paused=!paused;Time.timeScale=paused?0:1; }
            if(!paused) { economy.step(Time.deltaTime);production.step(Time.deltaTime,finish_production);supply.step(current);siege.step(Time.deltaTime,current,construction,map.base_center);current.step(Time.time,Time.deltaTime); }
            if(Time.time>=next_fog) { next_fog=Time.time+.1f;current_fog.update_visibility(current); }
            if(Input.GetKeyDown(KeyCode.H))hud.show_help=!hud.show_help;
            if(Input.GetKeyDown(KeyCode.Z))hud.show_roster=!hud.show_roster;
            if(Input.GetKeyDown(KeyCode.B)&&!paused)select_headquarters(false);
            if(Input.GetKeyDown(KeyCode.T)&&!paused)toggle_selected_weapon();
            if(Input.GetKeyDown(KeyCode.V))reveal_map=!reveal_map;
            if(Input.GetKeyDown(KeyCode.C)){Camera.main.orthographicSize=22;focus_camera(input.selection_center());}
            if(Input.GetKeyDown(KeyCode.Home)){Camera.main.orthographicSize=27;focus_camera(map.base_center);}
            if(Input.GetKeyDown(KeyCode.F)){Camera.main.orthographicSize=140;focus_camera(Vector3.zero);}
            Camera.main.orthographicSize=Mathf.Clamp(Camera.main.orthographicSize-Input.mouseScrollDelta.y*2,8,145);
            Vector2 pan=new Vector2((Input.GetKey(KeyCode.RightArrow)?1:0)-(Input.GetKey(KeyCode.LeftArrow)?1:0),
                (Input.GetKey(KeyCode.UpArrow)?1:0)-(Input.GetKey(KeyCode.DownArrow)?1:0));
            Vector2 pointer=has_gui_pointer?gui_pointer:(Vector2)Input.mousePosition;
            Vector2 edge=RtsCameraPan.edge_direction(pointer,new Vector2(Screen.width,Screen.height),Application.isFocused);
            if(input.minimap_dragging||input.pointer_over_minimap(pointer))edge=Vector2.zero;
            game_cursor.update(edge,Application.isFocused,input.pending=="Attack");
            pan+=edge;
            pan=Vector2.ClampMagnitude(pan,1);
            if(pan.sqrMagnitude>0)focus_camera(camera_focus+RtsCameraPan.world_direction(pan,Camera.main.transform.rotation)*Camera.main.orthographicSize*Time.unscaledDeltaTime);
            construction.update();
            battle_audio.update(current,current_fog,reveal_map,camera_focus,Camera.main,paused);
            battle_view.draw(current,current_fog,reveal_map);
            if(!reveal_map)current_fog.draw();
        }
        private void OnGUI()
        {
            // Same drawable-window coordinates as selection/commands, including Retina resize and window borders.
            if(Event.current.isMouse||Event.current.type==EventType.Repaint)
            {gui_pointer=new Vector2(Event.current.mousePosition.x,Screen.height-Event.current.mousePosition.y);has_gui_pointer=true;}
            if(economy!=null)hud?.draw();
        }
        private void OnApplicationFocus(bool focused){has_gui_pointer=false;if(!focused)game_cursor?.update(Vector2.zero,false,false);}
        public void select_headquarters(bool focus=true)
        {
            construction?.cancel_preview();
            headquarters_selected=true;System.Array.Clear(current.selected,0,current.selected.Length);
            input.cancel_command();if(focus){focus_camera(map.headquarters_position);Camera.main.orthographicSize=20;}
        }
        private bool try_select_headquarters(Vector2 mouse)
        {
            var ray=Camera.main.ScreenPointToRay(new Vector3(mouse.x,Screen.height-mouse.y,0));
            if(!new Bounds(map.headquarters_position+Vector3.up*2.5f,new Vector3(10,5,8)).IntersectRay(ray))return false;
            select_headquarters();return true;
        }
        public bool finish_production(HeadquartersRecipe recipe)
        {
            if(recipe.is_unit)
            {
                for(int i=0;i<48;i++)
                {
                    var point=map.headquarters_position+new Vector3(-4+i%8*1.1f,0,-6-i/8*1.1f);
                    if(!map.blocked(point,.5f)&&current.recruit_soldier(point,recipe.recruit_id))return true;
                }
                return false;
            }
            construction.finish(recipe);return true;
        }
        public void toggle_selected_weapon()
        {
            bool use_knife=false;
            for(int i=0;i<current.soldier_count;i++)if(current.selected[i]&&current.health[i]>0&&!current.manual_melee[i]){use_knife=true;break;}
            for(int i=0;i<current.soldier_count;i++)if(current.selected[i]&&current.health[i]>0)current.manual_melee[i]=use_knife;
        }
        public void switch_visual_style(int index)
        {
            if(index==VisualStyles.index)return;
            construction.cancel_preview();VisualStyles.select(index);
            var old_landscape=landscape;
            landscape=new FrontierLandscape(map,transform,landscape_shader);
            construction.rebuild_visuals(landscape);structures.rebuild_visuals(landscape);old_landscape.Dispose();
            battle_view.set_style();configure_lighting();focus_camera(camera_focus);
            Debug.Log("[ArtStyle] "+VisualStyles.current.id+" switched; battle, economy, buildings, orders and fog retained");
        }
        private static void configure_lighting()
        {
            var style=VisualStyles.current;
            RenderSettings.ambientMode=UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor=style.color(style.sky)*.65f;
            RenderSettings.ambientEquatorColor=style.color(style.sky)*.4f;
            RenderSettings.ambientGroundColor=style.color(style.grass)*.27f;
            Camera.main.backgroundColor=style.color(style.sky)*.35f;
            foreach(var light in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
                if(light.type==LightType.Directional)
                {light.color=style.color(style.sunlight);light.intensity=style.sun_intensity;light.transform.rotation=Quaternion.Euler(style.id=="dusk"?35:52,-35,0);light.shadows=LightShadows.Soft;light.shadowStrength=.78f;light.shadowBias=.05f;light.shadowNormalBias=.25f;}
            QualitySettings.shadows=ShadowQuality.All;QualitySettings.shadowDistance=245;
            QualitySettings.shadowResolution=UnityEngine.ShadowResolution.VeryHigh;QualitySettings.shadowCascades=4;
        }
        private void OnDestroy()
        { Time.timeScale=1;if(previous_navigation_iterations>=0)UnityEngine.AI.NavMesh.pathfindingIterationsPerFrame=previous_navigation_iterations;game_cursor?.Dispose();battle_audio?.Dispose();structures?.Dispose();construction?.Dispose();battle_view?.Dispose();landscape?.Dispose();current_fog?.Dispose();current?.Dispose(); }
    }
}
