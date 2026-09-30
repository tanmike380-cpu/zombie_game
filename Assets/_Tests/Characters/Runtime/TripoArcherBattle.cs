using System;
using System.Collections;
using UnityEngine;
using ZombieGame.Balance;
using ZombieGame.Combat;
using ZombieGame.Controls;
using ZombieGame.Presentation;
using ZombieGame.Vision;

namespace ZombieGame.CharacterTests
{
    /// <summary>Population/layout-only fixture. Movement, damage, ammunition and controls use production systems.</summary>
    public sealed class TripoArcherBattle : MonoBehaviour, IRtsBattleView
    {
        public CharacterFrames archer_frames;
        public CharacterFrames exploder_frames;
        public bool exploder_fixture { get; private set; }
        public ExplosionFeedback explosions { get; private set; }
        public Material fog_template;
        public bool original_rest_only;
        public string human_unit_id = "archer";
        public bool frustum_culling;
        public bool kiting_fixture { get; private set; }
        public int sample_humans = 6, sample_zombies = 6;
        public BattleSimulation current { get; private set; }
        public CombatFog current_fog { get; private set; }
        public bool can_control => current != null && !benchmarking;
        public bool reveal_map => true;
        public Vector3 camera_focus { get; private set; }
        public bool benchmarking;
        public bool advance_simulation = true;
        public CharacterPose? forced_pose;
        public string status = "Tripo archer: original texture + Blender animations";
        public int visual_count { get; private set; }
        public int charging_zombies { get; private set; }
        public int attacking_zombies { get; private set; }
        public CharacterCrowdRenderer crowd_renderer { get; private set; }
        private Vector3[] visual_positions;
        private UnitFeedbackOverlay feedback;
        private RtsBattleInput controls;
        private RtsCursor cursor;
        private float next_fog;
        private float smoothed_ms;
        private Material arrow_material;
        private Mesh arrow_mesh;
        private readonly Matrix4x4[] arrow_matrices = new Matrix4x4[1023];

        private void Start()
        {
            Application.runInBackground = true;
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = -1;
            feedback = new UnitFeedbackOverlay();
            cursor = new RtsCursor();
            controls = gameObject.AddComponent<RtsBattleInput>();
            controls.game = this;
            create_arrow_visual();
            explosions = new ExplosionFeedback(Shader.Find("Standard"));
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-tripoChaseSmoke") >= 0)
            { benchmarking = true; advance_simulation = false; StartCoroutine(CrowdSpacingChecks.run_chase()); return; }
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-tripoSpacingSmoke") >= 0)
            { benchmarking = true; advance_simulation = false; StartCoroutine(CrowdSpacingChecks.run()); return; }
            var benchmark = gameObject.AddComponent<TripoArcherBenchmark>();
            benchmark.game = this;
            exploder_fixture = Array.IndexOf(Environment.GetCommandLineArgs(), "-tripoExploders") >= 0;
            kiting_fixture = exploder_fixture || Array.IndexOf(Environment.GetCommandLineArgs(), "-tripoKiting") >= 0;
            if (kiting_fixture)
            {
                if (original_rest_only || human_unit_id != "heavy_crossbowman")
                    throw new InvalidOperationException("Kiting fixture requires the Tripo crossbow review build");
                if (exploder_fixture && exploder_frames == null) throw new InvalidOperationException("Import the user's Tripo exploder first");
                sample_humans = 400; sample_zombies = exploder_fixture ? 4000 : 2000; frustum_culling = true;
                reset_population(sample_humans, sample_zombies, false);
            }
            else if (Array.IndexOf(Environment.GetCommandLineArgs(), "-tripoBenchmark") >= 0)
                benchmark.begin();
            else
            {
                reset_population(original_rest_only ? 10000 : sample_humans, original_rest_only ? 0 : sample_zombies, original_rest_only);
                if (original_rest_only) status = "Original Tripo model baseline — no animation, AI or navigation";
                if (Array.IndexOf(Environment.GetCommandLineArgs(), "-tripoSmoke") >= 0)
                    benchmark.begin_smoke();
            }
        }

        public void reset_population(int humans, int zombies, bool rendering_only)
        {
            var previous_kiting = GetComponent<TripoKitingBattle>();
            if (previous_kiting != null) { previous_kiting.enabled = false; Destroy(previous_kiting); }
            advance_simulation = true;
            Time.timeScale = 1;
            current?.Dispose(); current = null;
            current_fog?.Dispose(); current_fog = null;
            forced_pose = null;
            visual_count = humans;
            int total = humans + zombies;
            visual_positions = new Vector3[total];
            var ids = new string[total];
            int columns = humans < 100 ? 3 : 100;
            float spacing = rendering_only ? 1.05f : UnitBalance.config.formation_spacing;
            for (int i = 0; i < humans; i++)
            {
                visual_positions[i] = new Vector3((i % columns - (columns - 1) * .5f) * spacing,
                    0, humans < 100 ? -3 - i / columns * spacing : (i / columns - 49.5f) * spacing);
                ids[i] = human_unit_id;
            }
            for (int i = 0; i < zombies; i++)
            {
                visual_positions[humans + i] = humans < 100
                    ? new Vector3((i % 3 - 1) * spacing, 0, 3 + i / 3 * spacing)
                    : new Vector3((i % 100 - 49.5f) * 1.05f, 0, 55 + i / 100 * 1.1f);
                ids[humans + i] = exploder_fixture ? "exploder" : "walker";
            }
            if (kiting_fixture) TripoKitingBattle.fill_layout(visual_positions, humans);
            crowd_renderer = new CharacterCrowdRenderer(total, null, archer_frames, human_unit_id, exploder_fixture ? exploder_frames : null);
            if (!rendering_only)
            {
                var explosive = new bool[total];
                if (exploder_fixture) for (int i = humans; i < total; i++) explosive[i] = true;
                current = new BattleSimulation(visual_positions, humans, explosive, Array.Empty<Bounds>(), global_assault: kiting_fixture, unit_ids: ids);
                current_fog = new CombatFog(fog_template);
                current_fog.update_visibility(current);
                current.player_visibility = current_fog.is_visible;
                if (humans < 100)
                    for (int i = 0; i < humans; i++)
                    {
                        current.selected[i] = true;
                        current.issue_order(i, SoldierOrder.AttackMove, new Vector3(visual_positions[i].x, 0, 4));
                    }
            }
            controls.cancel_pointer_capture();
            controls.clear_groups();
            controls.notice = "F2 select all | Right-click move | A + left-click attack | R restart | F9 benchmark";
            Camera.main.orthographicSize = humans < 100 ? 8 : 84;
            focus_camera(Vector3.zero);
            if (kiting_fixture)
            {
                Camera.main.orthographicSize = 24;
                gameObject.AddComponent<TripoKitingBattle>().initialize(this);
            }
        }

        private void Update()
        {
            smoothed_ms = Mathf.Lerp(smoothed_ms, Time.unscaledDeltaTime * 1000, .08f);
            if (!benchmarking && Input.GetKeyDown(KeyCode.R)) reset_population(original_rest_only ? 10000 : sample_humans, original_rest_only ? 0 : sample_zombies, original_rest_only);
            if (!benchmarking && Input.GetKeyDown(KeyCode.F9)) GetComponent<TripoArcherBenchmark>().begin();
            if (current != null && advance_simulation)
            {
                current.step(Time.time, Time.deltaTime);
                if (Time.time >= next_fog)
                { current_fog.update_visibility(current); next_fog = Time.time + .25f; }
            }
            if (crowd_renderer == null) return;
            charging_zombies=attacking_zombies=0;
            crowd_renderer.begin_frame(frustum_culling ? Camera.main : null);
            for (int i = 0; i < visual_positions.Length; i++) draw_unit(i);
            crowd_renderer.draw();
            if (current != null)
            {
                feedback.draw(current, current_fog, true, Camera.main);
                draw_arrows();
                explosions.draw(current, current_fog, true);
            }
            update_camera();
        }

        private void draw_unit(int index)
        {
            if (current != null && current.health[index] <= 0) return;
            bool human = index < visual_count;
            Vector3 position = current == null ? visual_positions[index] : current.positions[index];
            var pose = forced_pose ?? CharacterPose.Idle;
            float age = Time.time + index * .071f;
            Quaternion rotation = Quaternion.identity;
            if (current != null)
            {
                var agent = current.crowd.agents[index];
                if (agent.enabled && agent.velocity.sqrMagnitude > .04f) pose = CharacterPose.Run;
                float attack_age = Time.time - current.attack_started_at[index];
                float interval = current.stats_for(index).attack_interval;
                if (attack_age < interval && pose != CharacterPose.Run)
                { pose = CharacterPose.Attack; age = attack_age / interval * archer_frames.poses[2].duration; }
                if(!human)
                {
                    pose=ZombieAnimation.choose_pose(current,index,Time.time);
                    if(pose==CharacterPose.Charge) charging_zombies++;
                    if(pose==CharacterPose.Attack) attacking_zombies++;
                    if(pose==CharacterPose.Attack) age=attack_age;
                }
                Vector3 facing = human ? current.soldier_facing[index] : current.crowd.transforms[index].forward;
                if (facing.sqrMagnitude > .001f) rotation = Quaternion.LookRotation(facing);
            }
            crowd_renderer.add(human, pose, age, position, rotation, explosive: !human && exploder_fixture,
                model_scale: human || current == null ? 1 : current.stats_for(index).model_scale, human_id: human ? human_unit_id : null);
        }

        private void create_arrow_visual()
        {
            var primitive = GameObject.CreatePrimitive(PrimitiveType.Cube);
            arrow_mesh = primitive.GetComponent<MeshFilter>().sharedMesh;
            Destroy(primitive);
            arrow_material = new Material(Shader.Find("Standard")) { color = new Color(.75f, .57f, .25f), enableInstancing = true };
        }

        private void draw_arrows()
        {
            int count = 0;
            foreach (var shot in current.projectiles)
            {
                if (!shot.active) continue;
                Vector3 direction = current.positions[shot.target] + Vector3.up - shot.position;
                arrow_matrices[count++] = Matrix4x4.TRS(shot.position, Quaternion.LookRotation(direction), new Vector3(.025f, .025f, .5f));
                if (count == arrow_matrices.Length) { submit_arrows(count); count = 0; }
            }
            submit_arrows(count);
        }

        private void submit_arrows(int count)
        {
            if (count > 0) Graphics.DrawMeshInstanced(arrow_mesh, 0, arrow_material, arrow_matrices, count);
        }

        private void update_camera()
        {
            Vector2 edge = benchmarking || controls.pointer_over_minimap(Input.mousePosition) || controls.minimap_dragging
                ? Vector2.zero : RtsCameraPan.edge_direction(Input.mousePosition, new Vector2(Screen.width, Screen.height), Application.isFocused);
            cursor.update(edge, Application.isFocused, controls.pending == "Attack");
            if (benchmarking) return;
            Camera.main.orthographicSize = Mathf.Clamp(Camera.main.orthographicSize - Input.mouseScrollDelta.y, 2, 110);
            focus_camera(camera_focus + RtsCameraPan.world_direction(edge, Camera.main.transform.rotation) * Camera.main.orthographicSize * Time.unscaledDeltaTime);
        }

        public void focus_camera(Vector3 point)
        {
            camera_focus = new Vector3(Mathf.Clamp(point.x, -125, 125), 0, Mathf.Clamp(point.z, -125, 125));
            Camera.main.transform.position = camera_focus + new Vector3(0, 100, -90);
        }

        public bool pointer_over_ui(Vector2 point) => point.y > Screen.height - 76;
        public Color32 obstacle_color(int index) => new Color32(90, 85, 70, 255);

        private void OnGUI()
        {
            GUI.Box(new Rect(8, 8, Screen.width - 16, 64), "");
            GUI.Label(new Rect(20, 16, Screen.width - 40, 25), status + $" | {1000 / Mathf.Max(1, smoothed_ms):F1} FPS");
            string details = current == null ? $"{visual_count:N0} full-detail models; animation/render ONLY, no combat/nav"
                : $"Units {current.living_soldiers}/{visual_count} | Zombies {current.zombie_count-current.dead_zombies} | Shots {current.shots} | Hits {current.hits} | R restart";
            GUI.Label(new Rect(20, 40, Screen.width - 40, 25), details);
        }

        private void OnDestroy()
        {
            Time.timeScale = 1;
            current?.Dispose(); current_fog?.Dispose(); feedback?.Dispose(); cursor?.Dispose();
            explosions?.Dispose();
            if (arrow_material != null) Destroy(arrow_material);
        }
    }
}
