using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;

namespace ZombieGame.PerformanceTests
{
    public sealed class NavMeshBenchmark : MonoBehaviour
    {
        public Material instance_template;
        private NavMeshCrowd crowd;
        private Mesh mesh;
        private Material units_material;
        private Material walls_material;
        private Matrix4x4[] matrices;
        private Matrix4x4[] wall_matrices;
        private readonly List<double> frames = new List<double>(100000);
        private readonly List<string> results = new List<string>();
        private string status = "Starting";
        private bool sampling;
        private bool running;
        private bool moving;
        private bool paused;
        private bool obstacles;
        private int old_iterations;
        private double last_time;
        private double average_ms;
        private double request_seconds;
        private double movement_start;
        private int gc_start;
        private string output_directory;

        private void Awake()
        {
            Application.runInBackground = true;
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = -1;
            old_iterations = NavMesh.pathfindingIterationsPerFrame;
            NavMesh.pathfindingIterationsPerFrame = 10000;
            var primitive = GameObject.CreatePrimitive(PrimitiveType.Cube);
            mesh = primitive.GetComponent<MeshFilter>().sharedMesh;
            Destroy(primitive);
            if (instance_template == null) throw new InvalidOperationException("Missing serialized instanced material");
            units_material = new Material(instance_template);
            units_material.SetColor("_Color", new Color(.9f, .12f, .05f));
            walls_material = new Material(instance_template);
            walls_material.SetColor("_Color", new Color(.15f, .6f, .85f));
            output_directory = Path.Combine(Application.persistentDataPath, "NavMeshBenchmark", DateTime.Now.ToString("yyyyMMdd-HHmmss"));
            Directory.CreateDirectory(output_directory);
            File.WriteAllText(Path.Combine(output_directory, "hardware.txt"), $"{SystemInfo.operatingSystem}\n{SystemInfo.processorType}\n{SystemInfo.graphicsDeviceName}\nUnity {Application.unityVersion}\nMedium avoidance; radius .25; speed 12; path iterations/frame 10000\n");
            last_time = Time.realtimeSinceStartupAsDouble;
            StartCoroutine(run_suite());
        }

        private IEnumerator run_suite()
        {
            running = true;
            results.Clear();
            results.Add("obstacles,count,width,height,setup_ms,path_ready_seconds,frames,average_ms,p95_ms,p99_ms,gc0_collections,arrived_at_sample_end,arrived_final,pending_final,invalid_final,geometry_errors,elapsed_movement_seconds");
            foreach (bool walls in new[] { false, true })
                foreach (int count in new[] { 5000, 10000 })
                {
                    yield return prepare_crowd(count, walls);
                    yield return measure_case();
                }
            running = false;
            status = "Tests complete. R: replay 10K walls | Space: pause | F: overview";
            Debug.Log("[NavMeshSuite] COMPLETE " + output_directory);
        }

        private IEnumerator prepare_crowd(int count, bool walls)
        {
            sampling = moving = false;
            status = "Building NavMesh and spawning " + count;
            if (crowd != null) { crowd.Dispose(); crowd = null; }
            yield return null;
            obstacles = walls;
            crowd = new NavMeshCrowd(count, walls);
            matrices = new Matrix4x4[count];
            wall_matrices = new Matrix4x4[crowd.walls.Length];
            for (int i = 0; i < wall_matrices.Length; i++)
                wall_matrices[i] = Matrix4x4.TRS(crowd.walls[i].center, Quaternion.identity, crowd.walls[i].size);
            double request_start = Time.realtimeSinceStartupAsDouble;
            status = "Waiting for native path requests (agents held still)";
            do
            {
                yield return null;
                crowd.update_counters(false);
            } while (crowd.ready_count < count && Time.realtimeSinceStartupAsDouble - request_start < 30);
            request_seconds = Time.realtimeSinceStartupAsDouble - request_start;
            Debug.Log($"[NavMeshSuite] READY walls={walls} count={count} ready={crowd.ready_count} pending={crowd.pending_count} invalid={crowd.invalid_paths} setup_ms={crowd.setup_ms:F1} request_s={request_seconds:F2}");
            crowd.set_paused(false);
            moving = true;
            paused = false;
            movement_start = Time.realtimeSinceStartupAsDouble;
        }

        private IEnumerator measure_case()
        {
            status = "Moving: 5s warmup, then 20s frame sample; arrival timeout 90s";
            while (Time.realtimeSinceStartupAsDouble - movement_start < 5) yield return null;
            frames.Clear();
            gc_start = GC.CollectionCount(0);
            sampling = true;
            while (Time.realtimeSinceStartupAsDouble - movement_start < 25) yield return null;
            sampling = false;
            int sample_arrived = crowd.arrived_count;
            int gc_count = GC.CollectionCount(0) - gc_start;
            frames.Sort();
            double total = 0;
            foreach (double value in frames) total += value;
            average_ms = total / Math.Max(1, frames.Count);
            double p95 = percentile(.95);
            double p99 = percentile(.99);
            Debug.Log($"[NavMeshSuite] SAMPLE walls={obstacles} count={crowd.agents.Length} avg_ms={average_ms:F3} p95={p95:F3} p99={p99:F3} arrived={sample_arrived}");
            status = "Sample saved; observing arrivals until all exit or 90s timeout";
            while (crowd.arrived_count < crowd.agents.Length && Time.realtimeSinceStartupAsDouble - movement_start < 90) yield return null;
            string row = string.Join(",", obstacles, crowd.agents.Length, Screen.width, Screen.height,
                number(crowd.setup_ms), number(request_seconds), frames.Count, number(average_ms), number(p95), number(p99),
                gc_count, sample_arrived, crowd.arrived_count, crowd.pending_count, crowd.invalid_paths, crowd.geometry_errors,
                number(Time.realtimeSinceStartupAsDouble - movement_start));
            results.Add(row);
            File.WriteAllLines(Path.Combine(output_directory, "results.csv"), results);
            Debug.Log("[NavMeshSuite] RESULT " + row);
        }

        private double percentile(double fraction) => frames.Count == 0 ? double.NaN : frames[(int)((frames.Count - 1) * fraction)];
        private static string number(double value) => value.ToString("F3", CultureInfo.InvariantCulture);

        private void Update()
        {
            double now = Time.realtimeSinceStartupAsDouble;
            double interval = (now - last_time) * 1000;
            last_time = now;
            if (sampling) frames.Add(interval);
            if (crowd == null) return;
            crowd.update_counters(moving);
            draw_crowd();
            if (!running && Input.GetKeyDown(KeyCode.R)) StartCoroutine(replay());
            if (!running && Input.GetKeyDown(KeyCode.Space))
            {
                paused = !paused;
                crowd.set_paused(paused);
            }
            if (running) return;
            Camera camera = Camera.main;
            camera.orthographicSize = Mathf.Clamp(camera.orthographicSize - Input.mouseScrollDelta.y * 5, 10, 160);
            camera.transform.position += new Vector3(Input.GetAxisRaw("Horizontal"), 0, Input.GetAxisRaw("Vertical")) * camera.orthographicSize * Time.unscaledDeltaTime;
            if (Input.GetKeyDown(KeyCode.F)) { camera.orthographicSize = 143; camera.transform.position = new Vector3(0, 200, 0); }
        }

        private IEnumerator replay()
        {
            running = true;
            yield return prepare_crowd(10000, true);
            running = false;
            status = "10K NavMesh replay | Space pause | R restart | WASD pan | Wheel zoom | F overview";
        }

        private void draw_crowd()
        {
            for (int i = 0; i < matrices.Length; i++)
                matrices[i] = Matrix4x4.TRS(crowd.transforms[i].position + Vector3.up * .6f, Quaternion.identity, new Vector3(.45f, 1.2f, .45f));
            draw_batches(units_material, matrices);
            draw_batches(walls_material, wall_matrices);
        }

        private void draw_batches(Material material, Matrix4x4[] instances)
        {
            var parameters = new RenderParams(material) { worldBounds = new Bounds(Vector3.zero, new Vector3(260, 20, 260)), shadowCastingMode = ShadowCastingMode.Off, receiveShadows = false };
            for (int start = 0; start < instances.Length; start += 1023)
                Graphics.RenderMeshInstanced(parameters, mesh, 0, instances, Math.Min(1023, instances.Length - start), start);
        }

        private void OnGUI()
        {
            GUI.matrix = Matrix4x4.Scale(Vector3.one * Mathf.Max(1, Screen.height / 700f));
            GUI.Box(new Rect(8, 8, 650, 130), "UNITY NAVMESH | 5K / 10K | Medium avoidance ON | 256 x 256");
            if (crowd == null) { GUI.Label(new Rect(20, 35, 620, 25), status); return; }
            GUI.Label(new Rect(20, 35, 620, 25), $"Units {crowd.agents.Length} | Walls {obstacles} | Exit {crowd.arrived_count} | Pending {crowd.pending_count} | Invalid {crowd.invalid_paths}");
            GUI.Label(new Rect(20, 60, 620, 25), $"Wall/bounds errors {crowd.geometry_errors} | Movement {Time.realtimeSinceStartupAsDouble - movement_start:F1}s | Last sample {average_ms:F2} ms/frame");
            GUI.Label(new Rect(20, 85, 620, 25), status);
            GUI.Label(new Rect(20, 110, 620, 25), "Red: native agents | Blue: walls | Exit: right edge (x >= 96); exited agents stop avoiding");
        }

        private void OnDestroy()
        {
            crowd?.Dispose();
            Destroy(units_material);
            Destroy(walls_material);
            NavMesh.pathfindingIterationsPerFrame = old_iterations;
        }
    }
}
