using UnityEngine;
using System.Collections.Generic;
using ZombieGame.Movement;

namespace ZombieGame.CombatStressTests
{
    /// <summary>RTS commands for instanced soldiers; mouse events use their actual GUI coordinates.</summary>
    public sealed class StressRtsInput : MonoBehaviour
    {
        public CombatStressBenchmark game;
        public string pending = "Select";
        public string notice = "400 selected. Right-click to move; A then left-click to attack.";
        private Vector2 drag_start;
        private bool dragging;
        private Texture2D minimap;
        private readonly Color32[] map_pixels = new Color32[256 * 256];
        private float next_map_refresh;
        private readonly FormationDestinations formation = new FormationDestinations();
        private readonly List<Vector3> selected_origins = new List<Vector3>(400), destinations = new List<Vector3>(400);
        private readonly bool[,] control_groups = new bool[10, CombatStressSimulation.SOLDIERS];
        private int last_group = -1;
        private float last_group_time = -1;
        private float scale => Mathf.Max(1, Screen.height / 800f);
        private Rect map_rect => new Rect(Screen.width - 226 * scale, Screen.height - 246 * scale, 210 * scale, 210 * scale);

        public void cancel_command() { pending = "Select"; dragging = false; }
        public void clear_groups()
        {
            System.Array.Clear(control_groups,0,control_groups.Length);
            last_group = -1; last_group_time = -1;
        }

        public void store_group(int group)
        {
            if (group < 0 || group > 9) return;
            for (int i = 0; i < CombatStressSimulation.SOLDIERS; i++)
                control_groups[group,i] = game.current.selected[i] && game.current.health[i] > 0;
            last_group = -1;
            notice = $"Group {group} saved: {selected_count()} soldiers.";
        }

        public void recall_group(int group, bool focus)
        {
            if (group < 0 || group > 9) return;
            for (int i = 0; i < CombatStressSimulation.SOLDIERS; i++)
                game.current.selected[i] = control_groups[group,i] && game.current.health[i] > 0;
            cancel_command();
            if (focus && selected_count() > 0)
            {
                Vector3 center = selection_center();
                Camera.main.transform.position = new Vector3(center.x,200,center.z);
            }
            notice = $"Group {group}: {selected_count()} soldiers" + (focus ? " / camera centered." : ".");
        }

        private void handle_group_keys()
        {
            if (Input.GetKeyDown(KeyCode.BackQuote) || Input.GetKeyDown(KeyCode.F2)) select_all();
            bool control = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
            for (int group = 0; group <= 9; group++)
            {
                if (!Input.GetKeyDown((KeyCode)((int)KeyCode.Alpha0 + group))) continue;
                if (control) store_group(group);
                else
                {
                    recall_group(group,last_group == group && Time.unscaledTime-last_group_time <= .35f);
                    last_group = group; last_group_time = Time.unscaledTime;
                }
            }
        }
        public void select_all()
        {
            if (game.current == null) return;
            for (int i = 0; i < CombatStressSimulation.SOLDIERS; i++) game.current.selected[i] = game.current.health[i] > 0;
            cancel_command(); notice = "All living soldiers selected. Right-click moves.";
        }
        public int selected_count()
        {
            int count = 0;
            if (game.current != null)
                for (int i = 0; i < 400; i++) if (game.current.health[i] > 0 && game.current.selected[i]) count++;
            return count;
        }
        public Vector3 selection_center()
        {
            Vector3 center = Vector3.zero; int count = 0;
            for (int i = 0; i < 400; i++)
                if (game.current.health[i] > 0 && game.current.selected[i]) { center += game.current.positions[i]; count++; }
            if (count > 0) return center / count;
            for (int i = 0; i < 400; i++)
                if (game.current.health[i] > 0) { center += game.current.positions[i]; count++; }
            return count > 0 ? center / count : new Vector3(-81,0,0);
        }

        private void Update()
        {
            if (game == null || game.current == null) return;
            if (Time.unscaledTime >= next_map_refresh) { next_map_refresh = Time.unscaledTime + .15f; refresh_minimap(); }
            if (!game.can_control) { dragging = false; return; }
            handle_group_keys();
            if (Input.GetKeyDown(KeyCode.Escape)) cancel_command();
            if (Input.GetKeyDown(KeyCode.A))
            {
                if (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl) || Input.GetKey(KeyCode.LeftCommand)) select_all();
                else arm("Attack");
            }
            if (Input.GetKeyDown(KeyCode.M)) arm("Move");
            if (Input.GetKeyDown(KeyCode.Q)) arm("Patrol");
            if (Input.GetKeyDown(KeyCode.S)) { issue_selected(SoldierOrder.Stop,Vector3.zero); cancel_command(); }
        }

        private void arm(string command)
        {
            if (selected_count() == 0) { notice = "Select blue soldiers first."; return; }
            pending = command; dragging = false; notice = command + ": left-click destination / visible enemy.";
        }

        public int issue_selected(SoldierOrder order, Vector3 point, int target = -1)
        {
            int issued = 0, slot = 0;
            bool group_move = order == SoldierOrder.Move || order == SoldierOrder.AttackMove || order == SoldierOrder.Patrol;
            if (group_move)
            {
                selected_origins.Clear();
                for (int i = 0; i < 400; i++)
                    if (game.current.selected[i] && game.current.health[i] > 0) selected_origins.Add(game.current.positions[i]);
                if (!formation.build(point,selected_origins,destinations))
                { notice = "No reachable formation area near the click."; return 0; }
            }
            for (int i = 0; i < 400; i++)
            {
                if (!game.current.selected[i] || game.current.health[i] <= 0) continue;
                Vector3 goal = group_move ? destinations[slot++] : point;
                if (game.current.issue_order(i,order,goal,target)) issued++;
            }
            notice = issued > 0 ? $"{order}: {issued} soldiers ordered." : "No valid order: select soldiers / choose reachable ground.";
            return issued;
        }

        private bool world_point(Vector2 gui_point, out Vector3 point)
        {
            Ray ray = Camera.main.ScreenPointToRay(new Vector3(gui_point.x,Screen.height-gui_point.y,0));
            if (new Plane(Vector3.up,Vector3.zero).Raycast(ray,out float length)) { point = ray.GetPoint(length); return true; }
            point = Vector3.zero; return false;
        }

        public int pick_unit(Vector2 gui_point, bool enemies)
        {
            int best = -1; float closest = 14 * scale;
            int first = enemies ? 400 : 0, end = enemies ? CombatStressSimulation.TOTAL : 400;
            for (int i = first; i < end; i++)
            {
                if (game.current.health[i] <= 0 || (enemies && !game.current_fog.is_visible(game.current.positions[i]))) continue;
                Vector3 screen = Camera.main.WorldToScreenPoint(game.current.positions[i] + Vector3.up * .6f);
                if (screen.z <= 0) continue;
                float distance = Vector2.Distance(gui_point,new Vector2(screen.x,Screen.height-screen.y));
                if (distance < closest) { closest = distance; best = i; }
            }
            return best;
        }

        private void issue_click(Vector2 gui_point, Vector3 point, bool minimap_click)
        {
            int target = pending == "Attack" && !minimap_click ? pick_unit(gui_point,true) : -1;
            SoldierOrder order = pending == "Attack" ? target >= 0 ? SoldierOrder.AttackTarget : SoldierOrder.AttackMove
                : pending == "Patrol" ? SoldierOrder.Patrol : SoldierOrder.Move;
            issue_selected(order,point,target); cancel_command();
        }

        public void select_rectangle(Vector2 start, Vector2 end, bool additive)
        {
            if (!additive) for (int i = 0; i < 400; i++) game.current.selected[i] = false;
            if (Vector2.Distance(start,end) < 6 * scale)
            {
                int unit = pick_unit(end,false); if (unit >= 0) game.current.selected[unit] = true;
            }
            else
            {
                Rect box = Rect.MinMaxRect(Mathf.Min(start.x,end.x),Mathf.Min(start.y,end.y),Mathf.Max(start.x,end.x),Mathf.Max(start.y,end.y));
                for (int i = 0; i < 400; i++)
                {
                    Vector3 p = Camera.main.WorldToScreenPoint(game.current.positions[i] + Vector3.up * .6f);
                    if (game.current.health[i] > 0 && p.z > 0 && box.Contains(new Vector2(p.x,Screen.height-p.y))) game.current.selected[i] = true;
                }
            }
            notice = $"{selected_count()} selected. Right-click moves; A + left-click attacks.";
        }

        public void handle_mouse(Event input)
        {
            Vector2 mouse = input.mousePosition;
            if (input.type == EventType.MouseDown && map_rect.Contains(mouse))
            {
                Vector3 point = new Vector3((mouse.x-map_rect.x)/map_rect.width*256-128,0,(1-(mouse.y-map_rect.y)/map_rect.height)*256-128);
                if (input.button == 1) { issue_selected(SoldierOrder.Move,point); cancel_command(); }
                else if (input.button == 0 && pending != "Select") issue_click(mouse,point,true);
                else if (input.button == 0) Camera.main.transform.position = new Vector3(point.x,200,point.z);
                dragging = false; input.Use(); return;
            }
            // Diagnostic text is non-interactive: selection may start/end beneath it.
            if (input.type == EventType.MouseDown)
            {
                if (input.button == 1 && world_point(mouse,out var move))
                { issue_selected(SoldierOrder.Move,move); cancel_command(); input.Use(); }
                else if (input.button == 0)
                {
                    if (pending != "Select" && world_point(mouse,out var goal)) issue_click(mouse,goal,false);
                    else { drag_start = mouse; dragging = true; }
                    input.Use();
                }
            }
            else if (input.type == EventType.MouseUp && input.button == 0 && dragging)
            {
                dragging = false;
                select_rectangle(drag_start,mouse,input.shift);
                input.Use();
            }
        }

        private void refresh_minimap()
        {
            if (game.current_fog == null) return;
            if (minimap == null) minimap = new Texture2D(256,256,TextureFormat.RGBA32,false) { filterMode = FilterMode.Point };
            for (int i = 0; i < map_pixels.Length; i++) map_pixels[i] = game.current_fog.minimap_color(i,game.reveal_map);
            foreach (var wall in game.current.crowd.walls)
                for (int z = Mathf.Max(0,Mathf.FloorToInt(wall.min.z+128)); z < Mathf.Min(256,Mathf.CeilToInt(wall.max.z+128)); z++)
                    for (int x = Mathf.Max(0,Mathf.FloorToInt(wall.min.x+128)); x < Mathf.Min(256,Mathf.CeilToInt(wall.max.x+128)); x++)
                        if (game.reveal_map || game.current_fog.is_explored(new Vector3(x-127.5f,0,z-127.5f)))
                            map_pixels[x+z*256] = wall.size.y < 1 ? new Color32(20,70,120,255) : new Color32(85,75,65,255);
            for (int i = 0; i < CombatStressSimulation.TOTAL; i++)
            {
                Vector3 p = game.current.positions[i];
                if (game.current.health[i] <= 0 || (i >= 400 && !game.reveal_map && !game.current_fog.is_visible(p))) continue;
                int x = Mathf.Clamp(Mathf.FloorToInt(p.x+128),0,255), z = Mathf.Clamp(Mathf.FloorToInt(p.z+128),0,255);
                map_pixels[x+z*256] = i < 400 ? new Color32(40,170,255,255) : game.current.exploder[i] ? new Color32(240,30,220,255) : new Color32(230,40,20,255);
            }
            minimap.SetPixels32(map_pixels); minimap.Apply(false,false);
        }

        private static void outline(Rect rect, Color color, float width = 2)
        {
            GUI.color = color;
            GUI.DrawTexture(new Rect(rect.x,rect.y,rect.width,width),Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(rect.x,rect.yMax-width,rect.width,width),Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(rect.x,rect.y,width,rect.height),Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(rect.xMax-width,rect.y,width,rect.height),Texture2D.whiteTexture);
            GUI.color = Color.white;
        }

        private void OnGUI()
        {
            if (game == null || game.current == null) return;
            GUI.matrix = Matrix4x4.identity;
            if (game.can_control) handle_mouse(Event.current);
            GUI.Box(new Rect(8,Screen.height-48*scale,Screen.width-250*scale,40*scale),
                $"{(game.can_control ? "YOU CONTROL" : "AUTO / CHECK")} | Selected {selected_count()} | {pending} | {notice}");
            if (minimap != null) GUI.DrawTexture(map_rect,minimap);
            outline(map_rect,Color.white);
            GUI.Label(new Rect(map_rect.x,map_rect.y-24*scale,map_rect.width,24*scale),"FULL MAP: 256 x 256 tiles");
            GUI.Label(new Rect(map_rect.x,map_rect.yMax+3,map_rect.width,25*scale),"Left: camera | Right: move units");
            Camera camera = Camera.main;
            float half_z = camera.orthographicSize, half_x = half_z*camera.aspect;
            Rect camera_box = Rect.MinMaxRect(
                map_rect.x+(camera.transform.position.x-half_x+128)/256*map_rect.width,
                map_rect.y+(128-camera.transform.position.z-half_z)/256*map_rect.height,
                map_rect.x+(camera.transform.position.x+half_x+128)/256*map_rect.width,
                map_rect.y+(128-camera.transform.position.z+half_z)/256*map_rect.height);
            camera_box = Rect.MinMaxRect(Mathf.Max(map_rect.x,camera_box.x),Mathf.Max(map_rect.y,camera_box.y),Mathf.Min(map_rect.xMax,camera_box.xMax),Mathf.Min(map_rect.yMax,camera_box.yMax));
            outline(camera_box,Color.yellow,1);
            Vector3 left_top = camera.WorldToScreenPoint(new Vector3(-128,0,128)), right_bottom = camera.WorldToScreenPoint(new Vector3(128,0,-128));
            outline(Rect.MinMaxRect(left_top.x,Screen.height-left_top.y,right_bottom.x,Screen.height-right_bottom.y),Color.white);
            if (dragging)
            {
                Vector2 mouse = Event.current.mousePosition;
                outline(Rect.MinMaxRect(Mathf.Min(drag_start.x,mouse.x),Mathf.Min(drag_start.y,mouse.y),Mathf.Max(drag_start.x,mouse.x),Mathf.Max(drag_start.y,mouse.y)),Color.green);
            }
        }

        private void OnDestroy() { if (minimap != null) Destroy(minimap); }
    }
}
