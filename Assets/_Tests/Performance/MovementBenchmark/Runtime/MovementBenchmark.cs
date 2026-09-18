using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;
using UnityEngine.Rendering;
using Stopwatch = System.Diagnostics.Stopwatch;

namespace ZombieGame.PerformanceTests
{
    public sealed class MovementBenchmark : MonoBehaviour
    {
        public Shader instance_shader;
        public Material instance_template;
        private HordeNavigation navigation;
        private HordeMovement movement;
        private Mesh cube_mesh;
        private Material unit_material;
        private Material wall_material;
        private Matrix4x4[] unit_matrices;
        private Matrix4x4[] wall_matrices;
        private readonly List<double> frame_samples = new List<double>(100000);
        private readonly List<string> results = new List<string>();
        private readonly int[] counts = { 1000, 5000, 10000 };
        private double previous_time;
        private double stage_start;
        private double simulation_ms;
        private double draw_ms;
        private float accumulator;
        private int stage;
        private bool is_running_suite;
        private bool is_paused;
        private bool has_obstacles = true;
        private string status = "Ready";
        private const float TICK_SECONDS = 0.05f;

        private void Awake()
        {
            Application.runInBackground = true;
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = -1;
            GameObject primitive = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube_mesh = primitive.GetComponent<MeshFilter>().sharedMesh;
            Destroy(primitive);
            unit_material = create_material(new Color(0.85f, 0.08f, 0.04f));
            wall_material = create_material(new Color(0.2f, 0.6f, 0.85f));
            reset_simulation(10000, true);
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-movementSuite") >= 0) start_suite();
            previous_time = Time.realtimeSinceStartupAsDouble;
        }

        private Material create_material(Color color)
        {
            if (instance_shader == null || !instance_shader.isSupported)
                throw new InvalidOperationException("Movement benchmark requires a supported instanced shader.");
            var material = new Material(instance_template) { enableInstancing = true };
            material.SetColor("_Color", color);
            return material;
        }

        private void reset_simulation(int count, bool obstacles)
        {
            has_obstacles = obstacles;
            navigation = new HordeNavigation(obstacles);
            movement = new HordeMovement(count, navigation);
            unit_matrices = new Matrix4x4[count];
            var walls = new List<Matrix4x4>();
            for (int cell = 0; cell < navigation.blocked.Length; cell++)
                if (navigation.blocked[cell]) walls.Add(Matrix4x4.TRS(
                    HordeNavigation.cell_position(cell), Quaternion.identity, new Vector3(1f, 2f, 1f)));
            wall_matrices = walls.ToArray();
            accumulator = 0f;
            is_paused = false;
        }

        private void Update()
        {
            double now = Time.realtimeSinceStartupAsDouble;
            double frame_ms = (now - previous_time) * 1000;
            previous_time = now;
            long start = Stopwatch.GetTimestamp();
            if (!is_paused)
            {
                accumulator += Time.unscaledDeltaTime;
                int ticks = 0;
                while (accumulator >= TICK_SECONDS && ticks++ < 8)
                {
                    movement.step(TICK_SECONDS);
                    accumulator -= TICK_SECONDS;
                }
            }
            double step_ms = elapsed_ms(start);
            start = Stopwatch.GetTimestamp();
            draw_horde();
            double submission_ms = elapsed_ms(start);
            if (is_running_suite) sample_suite(now, frame_ms, step_ms, submission_ms);
            else handle_input();
        }

        private static double elapsed_ms(long start)
        {
            return (Stopwatch.GetTimestamp() - start) * 1000.0 / Stopwatch.Frequency;
        }

        private void draw_horde()
        {
            for (int i = 0; i < unit_matrices.Length; i++)
                unit_matrices[i] = Matrix4x4.TRS(movement.positions[i], Quaternion.identity, new Vector3(0.5f, 1.2f, 0.5f));
            draw_batches(unit_material, unit_matrices);
            draw_batches(wall_material, wall_matrices);
        }

        private void draw_batches(Material material, Matrix4x4[] matrices)
        {
            var parameters = new RenderParams(material)
            {
                worldBounds = new Bounds(Vector3.zero, new Vector3(260, 20, 260)),
                shadowCastingMode = ShadowCastingMode.Off, receiveShadows = false
            };
            for (int start = 0; start < matrices.Length; start += 1023)
                Graphics.RenderMeshInstanced(parameters, cube_mesh, 0, matrices, Math.Min(1023, matrices.Length - start), start);
        }

        private void handle_input()
        {
            if (Input.GetKeyDown(KeyCode.Space)) is_paused = !is_paused;
            if (Input.GetKeyDown(KeyCode.R)) reset_simulation(movement.positions.Length, has_obstacles);
            Camera camera = Camera.main;
            camera.orthographicSize = Mathf.Clamp(camera.orthographicSize - Input.mouseScrollDelta.y * 5, 10, 160);
            Vector3 pan = new Vector3(Input.GetAxisRaw("Horizontal"), 0, Input.GetAxisRaw("Vertical"));
            camera.transform.position += pan * (camera.orthographicSize * Time.unscaledDeltaTime);
            if (Input.GetKeyDown(KeyCode.F))
            {
                camera.transform.position = new Vector3(0, 200, 0);
                camera.orthographicSize = 143;
            }
            if (!Input.GetMouseButtonDown(1)) return;
            Ray ray = camera.ScreenPointToRay(Input.mousePosition);
            if (!new Plane(Vector3.up, Vector3.zero).Raycast(ray, out float distance)) return;
            Vector3 point = ray.GetPoint(distance);
            int x = Mathf.FloorToInt(point.x + 128);
            int z = Mathf.FloorToInt(point.z + 128);
            if (x < 0 || x >= 256 || z < 0 || z >= 256 || !navigation.build_field(x + z * 256))
            {
                status = "Rejected: target outside map or inside wall";
                return;
            }
            movement.retarget();
            status = "Target updated";
        }

        private void start_suite()
        {
            results.Clear();
            results.Add("obstacles,count,width,height,frames,avg_frame_ms,p95_frame_ms,p99_frame_ms,avg_simulation_ms,avg_draw_ms,arrived,invalid");
            stage = 0;
            is_running_suite = true;
            begin_stage();
        }

        private void begin_stage()
        {
            reset_simulation(counts[stage % 3], stage >= 3);
            frame_samples.Clear();
            simulation_ms = draw_ms = 0;
            stage_start = Time.realtimeSinceStartupAsDouble;
            status = "Sampling stage " + (stage + 1) + "/6: 5s warmup + 20s sample";
            Debug.Log("[MovementSuite] " + status);
        }

        private void sample_suite(double now, double frame_ms, double step_ms, double submission_ms)
        {
            if (now - stage_start < 5) return;
            frame_samples.Add(frame_ms);
            simulation_ms += step_ms;
            draw_ms += submission_ms;
            if (now - stage_start < 25) return;
            double sum = 0;
            foreach (double sample in frame_samples) sum += sample;
            frame_samples.Sort();
            int count = frame_samples.Count;
            string row = string.Join(",", has_obstacles, movement.positions.Length, Screen.width, Screen.height, count,
                format_number(sum / count), format_number(frame_samples[(int)((count - 1) * .95)]),
                format_number(frame_samples[(int)((count - 1) * .99)]), format_number(simulation_ms / count),
                format_number(draw_ms / count), movement.arrived_count, movement.invalid_count);
            results.Add(row);
            Debug.Log("[MovementSuite] " + row);
            if (++stage < 6) { begin_stage(); return; }
            string folder = Path.Combine(Application.persistentDataPath, "MovementBenchmark");
            Directory.CreateDirectory(folder);
            File.WriteAllLines(Path.Combine(folder, "performance.csv"), results);
            File.WriteAllText(Path.Combine(folder, "hardware.txt"), SystemInfo.operatingSystem + "\n" + SystemInfo.processorType
                + "\n" + SystemInfo.graphicsDeviceName + "\nUnity " + Application.unityVersion);
            is_running_suite = false;
            reset_simulation(10000, true);
            status = "Suite saved: " + folder;
            Debug.Log("[MovementSuite] COMPLETE " + folder);
        }

        private static string format_number(double value) => value.ToString("F4", CultureInfo.InvariantCulture);

        private void OnGUI()
        {
            GUI.Box(new Rect(10, 10, 650, 165), "10K Movement / Shared BFS Navigation | 256 x 256");
            GUI.Label(new Rect(25, 38, 620, 25), $"Units {movement.positions.Length} | Arrived {movement.arrived_count} | Invalid {movement.invalid_count} | Walls {has_obstacles}");
            GUI.Label(new Rect(25, 63, 620, 25), "Right click: goal | Space: pause | R: reset | WASD/arrows: pan | Wheel: zoom | F: overview");
            GUI.Label(new Rect(25, 88, 620, 25), "Red: zombies | Blue: walls | Units overlap; no combat, animation or local avoidance");
            GUI.Label(new Rect(25, 113, 620, 25), status);
            if (!is_running_suite)
            {
                if (GUI.Button(new Rect(25, 140, 140, 25), "Run 6-case suite")) start_suite();
                if (GUI.Button(new Rect(175, 140, 140, 25), "Toggle obstacles")) reset_simulation(10000, !has_obstacles);
            }
            Vector3 goal = Camera.main.WorldToScreenPoint(HordeNavigation.cell_position(navigation.target_cell));
            GUI.Label(new Rect(goal.x - 20, Screen.height - goal.y - 20, 80, 25), "GOAL");
        }

        private void OnDestroy()
        {
            Destroy(unit_material);
            Destroy(wall_material);
        }
    }
}
