using UnityEngine;
using System.Collections.Generic;
using ZombieGame.Movement;
using ZombieGame.Balance;

namespace ZombieGame.CombatTests
{
    public sealed class RtsInput : MonoBehaviour
    {
        public CombatSandbox game;
        private string pending = "Select";
        private Vector2 drag_start;
        private bool dragging;
        private readonly FormationDestinations formation = new FormationDestinations();
        private readonly List<Vector3> origins = new List<Vector3>(), destinations = new List<Vector3>();
        private float ui_scale => Mathf.Max(1, Screen.height / 800f);
        private bool show_hud = true;
        private bool over_hud => !game.use_character_models && show_hud && Input.mousePosition.y > Screen.height - 140 * ui_scale;

        private void Update()
        {
            if (game == null || game.running_checks) return;
            if (Input.GetKeyDown(KeyCode.H)) show_hud = !show_hud;
            if (Input.GetKeyDown(KeyCode.F2) || Input.GetKeyDown(KeyCode.BackQuote))
                foreach (var actor in game.actors) actor.selected = actor.friendly && actor.alive;
            if (Input.GetKeyDown(KeyCode.R)) { game.reset_battle(); pending = "Select"; dragging = false; }
            if (Input.GetKeyDown(KeyCode.Escape)) { pending = "Select"; dragging = false; }
            if (Input.GetKeyDown(KeyCode.A)) arm_command("Attack");
            if (Input.GetKeyDown(KeyCode.Q)) arm_command("Patrol");
            if (Input.GetKeyDown(KeyCode.M)) arm_command("Move");
            if (Input.GetKeyDown(KeyCode.S)) { issue_selected(CombatOrder.Stop, Vector3.zero); pending = "Select"; }
            if (Input.GetMouseButtonDown(1) && !over_hud)
            {
                if (ground_point(out Vector3 point)) issue_selected(CombatOrder.Move, point);
                pending = "Select"; dragging = false;
            }
            if (Input.GetMouseButtonDown(0) && !over_hud)
            {
                if (pending != "Select") { apply_click_command(); dragging = false; }
                else { drag_start = Input.mousePosition; dragging = true; }
            }
            if (Input.GetMouseButtonUp(0) && dragging)
            {
                dragging = false;
                if (over_hud) return;
                if (pending == "Select") select_units(); else apply_click_command();
            }
            Camera.main.orthographicSize = Mathf.Clamp(Camera.main.orthographicSize - Input.mouseScrollDelta.y, 8, 22);
        }

        private int selected_count()
        {
            int count = 0;
            foreach (var actor in game.actors) if (actor.friendly && actor.alive && actor.selected) count++;
            return count;
        }

        private void arm_command(string command)
        {
            if (selected_count() == 0) { game.notice = "Select a Shenji first (left-click or drag a box)."; return; }
            pending = command;
            game.notice = command + ": left-click destination / enemy. Esc cancels.";
        }

        public void issue_selected(CombatOrder order, Vector3 point, CombatActor target = null)
        {
            int issued = 0, slot = 0;
            bool group_move = order == CombatOrder.Move || order == CombatOrder.AttackMove || order == CombatOrder.Patrol;
            if (group_move)
            {
                origins.Clear();
                foreach (var actor in game.actors) if (actor.friendly && actor.alive && actor.selected) origins.Add(actor.transform.position);
                if (!formation.build(point,origins,destinations)) { game.notice = "No valid formation area."; return; }
            }
            foreach (var actor in game.actors)
                if (actor.friendly && actor.alive && actor.selected && game.issue_order(actor, order, group_move ? destinations[slot++] : point, target)) issued++;
            game.notice = issued == 0 ? "No valid selected Shenji / destination." : order + " ordered for " + issued + " Shenji.";
        }

        private CombatActor picked_actor()
        {
            if (Physics.Raycast(Camera.main.ScreenPointToRay(Input.mousePosition), out var hit, 500))
                return hit.collider.GetComponentInParent<CombatActor>();
            return null;
        }

        private bool ground_point(out Vector3 point)
        {
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            if (new Plane(Vector3.up, Vector3.zero).Raycast(ray, out float distance))
            { point = ray.GetPoint(distance); return true; }
            point = Vector3.zero; return false;
        }

        private void apply_click_command()
        {
            if (!ground_point(out Vector3 point)) return;
            var actor = picked_actor();
            if (pending == "Attack")
            {
                if (actor != null && !actor.friendly && actor.alive) issue_selected(CombatOrder.AttackTarget, actor.transform.position, actor);
                else issue_selected(CombatOrder.AttackMove, point);
            }
            else issue_selected(pending == "Patrol" ? CombatOrder.Patrol : CombatOrder.Move, point);
            pending = "Select";
        }

        private void select_units()
        {
            bool additive = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
            if (!additive) foreach (var actor in game.actors) actor.selected = false;
            Vector2 end = Input.mousePosition;
            if (Vector2.Distance(end, drag_start) < 6)
            {
                var actor = picked_actor();
                if (actor != null && actor.friendly && actor.alive) actor.selected = true;
            }
            else
            {
                Rect box = selection_rect(drag_start, end);
                foreach (var actor in game.actors)
                {
                    Vector3 screen = Camera.main.WorldToScreenPoint(actor.transform.position + Vector3.up * .5f);
                    if (actor.friendly && actor.alive && screen.z > 0 && box.Contains(screen)) actor.selected = true;
                }
            }
            game.notice = selected_count() + " selected. Right-click moves; A then left-click attacks.";
        }

        public static Rect selection_rect(Vector2 a, Vector2 b) => Rect.MinMaxRect(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y), Mathf.Max(a.x, b.x), Mathf.Max(a.y, b.y));

        private void OnGUI()
        {
            if (game == null) return;
            GUI.matrix = Matrix4x4.Scale(Vector3.one * ui_scale);
            if (show_hud)
            {
            GUI.Box(new Rect(8, 8, 940, 125), $"RTS COMBAT | 8 Shenji ({UnitBalance.human.move_speed}) vs 36 Runner ({UnitBalance.runner.move_speed}) + 6 purple Exploder ({UnitBalance.exploder.move_speed})");
            GUI.Label(new Rect(20, 32, 910, 22), "Left click/drag: select | Right click: move | A + left: attack | Q + left: patrol | S: stop | M + left: move");
            GUI.Label(new Rect(20, 55, 910, 22), $"Esc: cancel | R: reset | Wheel: zoom | Gun range {CombatSandbox.ATTACK_RANGE} / noise {CombatSandbox.NOISE_RADIUS} | Low wall: shoot over; high wall: blocked");
            GUI.Label(new Rect(20, 78, 910, 22), $"Mode: {pending} | Selected {selected_count()} | Shots {game.shots} | Hits {game.hits} | Heard {game.heard} | Bites {game.bites} | Blasts {game.blasts} | Dead {game.kills}/{CombatSandbox.ZOMBIE_COUNT}");
            GUI.Label(new Rect(20, 101, 910, 22), game.running_checks ? "AUTO CHECK (~30 seconds): controls resume when complete. Please wait." : game.notice);
            }
            GUI.matrix = Matrix4x4.identity;
            foreach (var actor in game.actors)
            {
                if (!actor.alive) continue;
                Vector3 point = Camera.main.WorldToScreenPoint(actor.transform.position + Vector3.up * 1.9f);
                if (point.z <= 0) continue;
                float x = point.x - 25, y = Screen.height - point.y;
                GUI.color = Color.black; GUI.DrawTexture(new Rect(x, y, 50, 6), Texture2D.whiteTexture);
                GUI.color = actor.friendly ? Color.green : new Color(1, .25f, .15f);
                GUI.DrawTexture(new Rect(x + 1, y + 1, 48 * actor.health / actor.max_health, 4), Texture2D.whiteTexture);
                if (!actor.friendly && show_hud)
                {
                    GUI.color = actor.target != null ? Color.red : actor.has_memory ? Color.yellow : Color.white;
                    GUI.Label(new Rect(x - 15, y - 22, 95, 22), actor.detonate_at >= 0 ? "DETONATING!" : actor.target != null ? "CHASE" : actor.has_memory ? "INVESTIGATE" : "IDLE");
                }
            }
            GUI.color = Color.white;
            if (dragging && pending == "Select")
            {
                Rect box = selection_rect(drag_start, Input.mousePosition);
                box.y = Screen.height - box.yMax;
                GUI.color = new Color(.2f, 1, .4f, .2f); GUI.DrawTexture(box, Texture2D.whiteTexture); GUI.color = Color.white;
            }
            int friends = 0, enemies = 0;
            foreach (var actor in game.actors) if (actor.alive) { if (actor.friendly) friends++; else enemies++; }
            if (friends == 0 || enemies == 0)
                GUI.Box(new Rect(Screen.width / 2 - 170, Screen.height / 2 - 25, 340, 50), (friends == 0 ? "DEFEAT" : "VICTORY") + " - R to restart");
        }
    }
}
