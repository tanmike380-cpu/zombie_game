using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;
using UnityEngine.Rendering;

namespace ZombieGame.PerformanceTests
{
    public sealed class NoiseBenchmark : MonoBehaviour
    {
        public Material instance_template;
        private const int COUNT = 10000;
        private static readonly Vector3 SOURCE = new Vector3(2, 0, 0);
        private NavMeshCrowd crowd;
        private NoisePulseField pulse;
        private NoiseInvestigation[] memories;
        private Vector3[] starts;
        private Vector3[] idle_baseline;
        private Matrix4x4[] idle_matrices;
        private Matrix4x4[] active_matrices;
        private Matrix4x4[] wall_matrices;
        private Mesh mesh;
        private Material idle_material, active_material, wall_material, source_material, wave_material;
        private LineRenderer boundary, wave;
        private readonly List<double> frame_samples = new List<double>(100000);
        private int expected, triggered, outside_triggered, route_failures, wall_errors, idle_moved;
        private bool[] geometry_failed;
        private int settled;
        private double started_at, next_hearing_tick, last_frame, first_trigger, last_trigger;
        private bool emitted, saved;
        private Vector3[] silence_positions;
        private string result = "Waiting for pulse";

        private void Awake()
        {
            Application.runInBackground = true;
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = -1;
            var primitive = GameObject.CreatePrimitive(PrimitiveType.Cube);
            mesh = primitive.GetComponent<MeshFilter>().sharedMesh;
            Destroy(primitive);
            idle_material = create_material(new Color(.32f, .34f, .37f));
            active_material = create_material(new Color(.95f, .12f, .04f));
            wall_material = create_material(new Color(.12f, .55f, .85f));
            source_material = create_material(new Color(1, .85f, .05f));
            wave_material = create_material(new Color(.15f, 1, .55f));
            boundary = create_ring("10 tile boundary", source_material);
            wave = create_ring("Expanding sound front", wave_material);
            update_ring(boundary, 10);
            reset_test();
        }

        private Material create_material(Color color)
        {
            if (instance_template == null) throw new InvalidOperationException("Missing noise benchmark material");
            var material = new Material(instance_template) { enableInstancing = true };
            material.SetColor("_Color", color);
            return material;
        }

        private LineRenderer create_ring(string label, Material material)
        {
            var ring = new GameObject(label).AddComponent<LineRenderer>();
            ring.transform.SetParent(transform);
            ring.sharedMaterial = material;
            ring.useWorldSpace = true;
            ring.loop = true;
            ring.widthMultiplier = .05f;
            ring.positionCount = 128;
            return ring;
        }

        private void update_ring(LineRenderer ring, float radius)
        {
            for (int i = 0; i < 128; i++)
            {
                float angle = i * Mathf.PI * 2 / 128;
                ring.SetPosition(i, SOURCE + new Vector3(Mathf.Cos(angle) * radius, .05f, Mathf.Sin(angle) * radius));
            }
        }

        private void reset_test()
        {
            crowd?.Dispose();
            starts = new Vector3[COUNT];
            memories = new NoiseInvestigation[COUNT];
            geometry_failed = new bool[COUNT];
            idle_matrices = new Matrix4x4[COUNT];
            active_matrices = new Matrix4x4[COUNT];
            expected = triggered = outside_triggered = route_failures = wall_errors = idle_moved = settled = 0;
            for (int i = 0; i < COUNT; i++)
            {
                starts[i] = new Vector3(-104 + i % 100, 0, -50 + i / 100);
                memories[i] = new NoiseInvestigation();
                if (Vector3.Distance(starts[i], SOURCE) <= 10) expected++;
            }
            var walls = new[] {
                new Bounds(new Vector3(-2.5f, 1.5f, -1.5f), new Vector3(.5f, 3, 5)),
                new Bounds(new Vector3(-.5f, 1.5f, 1.5f), new Vector3(.5f, 3, 5))
            };
            crowd = new NavMeshCrowd(COUNT, true, starts, walls, false, ZombieGame.Balance.UnitBalance.get("walker"));
            idle_baseline = new Vector3[COUNT];
            for (int i = 0; i < COUNT; i++) idle_baseline[i] = crowd.transforms[i].position;
            Debug.Log($"[NoiseTest] Spawn snap requested={starts[0]:F5} native={idle_baseline[0]:F5}");
            wall_matrices = new Matrix4x4[walls.Length];
            for (int i = 0; i < walls.Length; i++) wall_matrices[i] = Matrix4x4.TRS(walls[i].center, Quaternion.identity, walls[i].size);
            pulse = new NoisePulseField();
            started_at = last_frame = Time.realtimeSinceStartupAsDouble;
            next_hearing_tick = started_at;
            first_trigger = last_trigger = -1;
            emitted = saved = false;
            silence_positions = null;
            frame_samples.Clear();
            result = "Pulse in 3 seconds; normal radius 10, NOT global attraction";
            set_close_camera();
            Debug.Log($"[NoiseTest] START population={COUNT} expected_in_radius={expected} radius={NoisePulseField.RADIUS} speed={ZombieGame.Balance.UnitBalance.get("walker").move_speed} agent_radius=.25");
        }

        private void Update()
        {
            double now = Time.realtimeSinceStartupAsDouble;
            if (!saved && now - started_at >= 3) frame_samples.Add((now - last_frame) * 1000);
            last_frame = now;
            if (!emitted && now - started_at >= 3)
            {
                pulse.emit(SOURCE, started_at + 3);
                emitted = true;
                result = "Pulse expanding; after it fades, red units remember the source";
            }
            while (next_hearing_tick <= now)
            {
                query_hearing(next_hearing_tick);
                next_hearing_tick += .05;
            }
            update_arrivals();
            if (silence_positions == null && now - started_at >= 6)
            {
                silence_positions = new Vector3[COUNT];
                for (int i = 0; i < COUNT; i++) silence_positions[i] = crowd.transforms[i].position;
            }
            draw_units();
            wave.enabled = pulse.has_live_wave(now);
            if (wave.enabled) update_ring(wave, pulse.wave_radius(now));
            if (!saved && now - started_at >= 28) save_result();
            if (Input.GetKeyDown(KeyCode.R)) reset_test();
            if (Input.GetKeyDown(KeyCode.N)) pulse.emit(SOURCE, now);
            if (Input.GetKeyDown(KeyCode.C)) set_close_camera();
            if (Input.GetKeyDown(KeyCode.F)) { Camera.main.transform.position = new Vector3(-48, 200, 0); Camera.main.orthographicSize = 62; }
            Camera.main.orthographicSize = Mathf.Clamp(Camera.main.orthographicSize - Input.mouseScrollDelta.y * 2, 4, 145);
        }

        private void query_hearing(double now)
        {
            for (int i = 0; i < COUNT; i++)
            {
                if (memories[i].has_memory) continue;
                int strength = pulse.sample_percent(starts[i], now);
                if (!memories[i].hear(SOURCE, strength)) continue;
                triggered++;
                double reaction = now - pulse.emitted_at;
                if (first_trigger < 0) first_trigger = reaction;
                last_trigger = reaction;
                if (Vector3.Distance(starts[i], SOURCE) > 10) outside_triggered++;
                if (!crowd.investigate_position(i, memories[i].remembered_position)) route_failures++;
            }
        }

        private void update_arrivals()
        {
            settled = 0;
            for (int i = 0; i < COUNT; i++)
            {
                if (!memories[i].has_memory) continue;
                Vector3 position = crowd.transforms[i].position;
                if (!geometry_failed[i])
                    foreach (Bounds wall in crowd.walls)
                        if (position.x > wall.min.x && position.x < wall.max.x && position.z > wall.min.z && position.z < wall.max.z)
                        { geometry_failed[i] = true; wall_errors++; break; }
                var agent = crowd.agents[i];
                if (!agent.isOnNavMesh || agent.pathPending) continue;
                if (agent.remainingDistance <= agent.stoppingDistance + .1f)
                {
                    agent.isStopped = true;
                    settled++;
                }
            }
        }

        private void draw_units()
        {
            int active_count = 0;
            int idle_count = 0;
            for (int i = 0; i < COUNT; i++)
            {
                bool active = memories[i].has_memory;
                Vector3 position = active ? crowd.transforms[i].position : starts[i];
                var matrix = Matrix4x4.TRS(position + Vector3.up * .6f, Quaternion.identity, new Vector3(.45f, 1.2f, .45f));
                if (active) active_matrices[active_count++] = matrix;
                else idle_matrices[idle_count++] = matrix;
            }
            draw_batches(idle_material, idle_matrices, idle_count);
            draw_batches(active_material, active_matrices, active_count);
            draw_batches(wall_material, wall_matrices, wall_matrices.Length);
            Graphics.DrawMesh(mesh, Matrix4x4.TRS(SOURCE + Vector3.up, Quaternion.identity, new Vector3(.6f, 2, .6f)), source_material, 0);
        }

        private void draw_batches(Material material, Matrix4x4[] matrices, int count)
        {
            var parameters = new RenderParams(material) { worldBounds = new Bounds(Vector3.zero, new Vector3(260, 20, 260)), shadowCastingMode = ShadowCastingMode.Off, receiveShadows = false };
            for (int start = 0; start < count; start += 1023)
                Graphics.RenderMeshInstanced(parameters, mesh, 0, matrices, Math.Min(1023, count - start), start);
        }

        private void save_result()
        {
            saved = true;
            for (int i = 0; i < COUNT; i++)
                if (!memories[i].has_memory && Vector3.Distance(crowd.transforms[i].position, idle_baseline[i]) > .001f) idle_moved++;
            frame_samples.Sort();
            double sum = 0;
            foreach (double frame in frame_samples) sum += frame;
            int continued_after_silence = 0;
            for (int i = 0; i < COUNT; i++)
                if (memories[i].has_memory && silence_positions != null
                    && Vector3.Distance(crowd.transforms[i].position, silence_positions[i]) > .1f) continued_after_silence++;
            bool passed = expected == triggered && outside_triggered == 0 && route_failures == 0 && wall_errors == 0 && idle_moved == 0 && continued_after_silence > 0;
            string report = FormattableString.Invariant($"pass={passed}\npopulation={COUNT}\nexpected_in_radius={expected}\ntriggered={triggered}\noutside_triggered={outside_triggered}\nroute_failures={route_failures}\nwall_errors={wall_errors}\nidle_moved={idle_moved}\nsettled_at_28s={settled}\nfirst_heard_seconds={first_trigger:F3}\nlast_heard_seconds={last_trigger:F3}\npulse_still_active={pulse.has_live_wave(Time.realtimeSinceStartupAsDouble)}\naverage_frame_ms={sum / frame_samples.Count:F3}\np95_frame_ms={frame_samples[(int)((frame_samples.Count - 1) * .95)]:F3}\np99_frame_ms={frame_samples[(int)((frame_samples.Count - 1) * .99)]:F3}\nresolution={Screen.width}x{Screen.height}\n");
            string directory = Path.Combine(Application.persistentDataPath, "NoiseBenchmark");
            report += "continued_after_silence=" + continued_after_silence + "\n";
            Directory.CreateDirectory(directory);
            File.WriteAllText(Path.Combine(directory, DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".txt"), report);
            result = (passed ? "PASS" : "FAIL") + ": range/trigger checks complete. R replay | N new pulse | F all units | C close";
            Debug.Log("[NoiseTest] RESULT\n" + report);
        }

        private void set_close_camera()
        {
            Camera.main.transform.position = new Vector3(-2, 200, 0);
            Camera.main.orthographicSize = 14;
        }

        private void OnGUI()
        {
            GUI.matrix = Matrix4x4.Scale(Vector3.one * Mathf.Max(1, Screen.height / 750f));
            GUI.Box(new Rect(8, 8, 690, 130), "NOISE: 10 tiles | 100% -> 10% | 10,000 present, only listeners move");
            GUI.Label(new Rect(20, 35, 670, 25), $"Expected {expected} | Heard {triggered} | Outside triggered {outside_triggered} | At source {settled}");
            GUI.Label(new Rect(20, 60, 670, 25), "Gray: idle | Red: heard / remembers source | Yellow: source + 10-tile boundary | Green: wave");
            GUI.Label(new Rect(20, 85, 670, 25), result);
            GUI.Label(new Rect(20, 110, 670, 25), "R reset + replay | N emit again | C close view | F overview | Wheel zoom");
        }

        private void OnDestroy()
        {
            crowd?.Dispose();
            Destroy(idle_material); Destroy(active_material); Destroy(wall_material); Destroy(source_material); Destroy(wave_material);
        }
    }
}
