using UnityEngine;
using ZombieGame.Combat;
using ZombieGame.Controls;
using ZombieGame.Vision;

namespace ZombieGame.World
{
    /// <summary>Playable world slice; no dependency on benchmark or test assemblies.</summary>
    public sealed class FrontierGame : MonoBehaviour, IRtsBattleView
    {
        public Material fog_template;
        public Shader landscape_shader;
        public BattleSimulation current {get;private set;}
        public CombatFog current_fog {get;private set;}
        public bool can_control => current!=null && !paused;
        public bool reveal_map {get;private set;}
        public Vector3 camera_focus {get;private set;}
        public FrontierMap map {get;private set;}
        public FrontierEconomy economy {get;private set;}
        private FrontierLandscape landscape;
        private FrontierBattleView battle_view;
        private RtsBattleInput input;
        private FrontierHud hud;
        public RtsBattleInput controls => input;
        public bool is_paused => paused;
        public string site_notice => resource_notice;
        private float next_fog;
        private bool paused;
        private string resource_notice="E: claim cleared resource site within 5 tiles";
        public bool pointer_over_ui(Vector2 point) => FrontierHud.contains(point);
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
            map=new FrontierMap();
            landscape=new FrontierLandscape(map,transform,landscape_shader);
            current=new BattleSimulation(map.spawns,FrontierMap.SOLDIERS,map.explosive,map.blockers);
            economy=new FrontierEconomy(JsonUtility.FromJson<FrontierEconomyConfig>(Resources.Load<TextAsset>("FrontierEconomy").text));
            current.try_supply_shot=economy.try_supply;
            current_fog=new CombatFog(fog_template,.08f);
            current_fog.explore_area(new Rect(-124,-124,76,76));
            current_fog.update_visibility(current);current.player_visibility=current_fog.is_visible;
            battle_view=new FrontierBattleView(current.total_count,transform,landscape_shader);
            input=gameObject.AddComponent<RtsBattleInput>();input.game=this;input.custom_command_panel=true;input.select_all();
            hud=new FrontierHud(this);
            Camera.main.orthographicSize=21;focus_camera(new Vector3(-91,0,-81));
            Debug.Log($"[Frontier] READY map=256x256 soldiers={current.soldier_count} zombies={current.zombie_count} blockers={map.blockers.Length} framebuffer={Screen.width}x{Screen.height}; economy provisional; only confirmed combat roles active");
        }
        public void focus_camera(Vector3 point)
        {
            camera_focus=new Vector3(Mathf.Clamp(point.x,-128,128),0,Mathf.Clamp(point.z,-128,128));
            Camera.main.transform.rotation=Quaternion.Euler(45,30,0);
            Camera.main.transform.position=camera_focus-Camera.main.transform.forward*180;
        }
        private void Update()
        {
            if(current==null)return;
            if(Input.GetKeyDown(KeyCode.Space)) { paused=!paused;Time.timeScale=paused?0:1; }
            if(!paused) { economy.step(Time.deltaTime);current.step(Time.time,Time.deltaTime); }
            if(Time.time>=next_fog) { next_fog=Time.time+.1f;current_fog.update_visibility(current); }
            if(Input.GetKeyDown(KeyCode.H))hud.show_help=!hud.show_help;
            if(Input.GetKeyDown(KeyCode.E)&&!paused)claim_site();
            if(Input.GetKeyDown(KeyCode.V))reveal_map=!reveal_map;
            if(Input.GetKeyDown(KeyCode.C)){Camera.main.orthographicSize=22;focus_camera(input.selection_center());}
            if(Input.GetKeyDown(KeyCode.Home)){Camera.main.orthographicSize=27;focus_camera(map.base_center);}
            if(Input.GetKeyDown(KeyCode.F)){Camera.main.orthographicSize=140;focus_camera(Vector3.zero);}
            Camera.main.orthographicSize=Mathf.Clamp(Camera.main.orthographicSize-Input.mouseScrollDelta.y*2,8,145);
            Vector3 pan=new Vector3((Input.GetKey(KeyCode.RightArrow)?1:0)-(Input.GetKey(KeyCode.LeftArrow)?1:0),0,
                (Input.GetKey(KeyCode.UpArrow)?1:0)-(Input.GetKey(KeyCode.DownArrow)?1:0));
            if(pan.sqrMagnitude>0)focus_camera(camera_focus+pan*Camera.main.orthographicSize*Time.unscaledDeltaTime);
            battle_view.draw(current,current_fog,reveal_map);
            if(!reveal_map)current_fog.draw();
        }
        private void OnGUI() { if(economy!=null)hud?.draw(); }
        public void claim_site()
        {
            foreach(var site in map.resource_sites)
            {
                if(site.claimed)continue;
                bool nearby=false,enemy=false;
                for(int i=0;i<current.total_count;i++)
                {
                    if(current.health[i]<=0)continue;
                    float distance=(current.positions[i]-site.position).sqrMagnitude;
                    if(i<current.soldier_count&&current.selected[i]&&distance<=25)nearby=true;
                    if(i>=current.soldier_count&&distance<=100)enemy=true;
                }
                if(!nearby||enemy)continue;
                site.claimed=true;
                switch(site.kind){case "Food":economy.food_sites++;break;case "Wood":economy.wood_sites++;break;case "Stone":economy.stone_sites++;break;case "Iron":economy.iron_sites++;break;}
                resource_notice=site.kind+" site claimed: income increased (prototype, no capture cost).";return;
            }
            resource_notice="Select troops within 5 tiles; clear zombies within 10 tiles.";
        }
        private void OnDestroy()
        { Time.timeScale=1;battle_view?.Dispose();landscape?.Dispose();current_fog?.Dispose();current?.Dispose(); }
    }
}
