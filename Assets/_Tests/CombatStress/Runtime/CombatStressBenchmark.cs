using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;
using UnityEngine.Rendering;

namespace ZombieGame.CombatStressTests
{
    public sealed class CombatStressBenchmark : MonoBehaviour
    {
        public Material instance_template;
        public Material fog_template;
        private CombatFog fog;
        private float next_fog_update;
        private bool spectator;
        private CombatStressSimulation simulation;
        private Mesh mesh;
        private readonly Material[] materials = new Material[9];
        private readonly Matrix4x4[][] matrices = new Matrix4x4[9][];
        private readonly int[] counts = new int[9];
        private readonly List<double> frames = new List<double>(20000), fighting_frames = new List<double>(20000);
        private readonly List<string> rows = new List<string>();
        private string status = "Preparing 400 vs 10,000", output_directory;
        private bool running, advancing, sampling;
        private double last_frame, started_at, setup_seconds;
        private float frame_ms;
        private int gc_start;

        private void Awake()
        {
            Application.runInBackground = true; QualitySettings.vSyncCount = 0; Application.targetFrameRate = -1;
            if (instance_template == null) throw new InvalidOperationException("Missing combat stress material");
            var primitive = GameObject.CreatePrimitive(PrimitiveType.Cube);
            mesh = primitive.GetComponent<MeshFilter>().sharedMesh; Destroy(primitive);
            Color[] colors = { new Color(.1f,.6f,1), new Color(.9f,.14f,.07f), new Color(.95f,.1f,.85f),
                new Color(.35f,.37f,.4f), new Color(.36f,.32f,.28f), Color.yellow, new Color(1,.4f,.85f),
                new Color(.16f,.25f,.12f), new Color(.08f,.3f,.55f) };
            for (int i = 0; i < materials.Length; i++)
            {
                materials[i] = new Material(instance_template) { enableInstancing = true };
                materials[i].SetColor("_Color", colors[i]); matrices[i] = new Matrix4x4[10400];
            }
            output_directory = Path.Combine(Application.persistentDataPath, "CombatStress", DateTime.Now.ToString("yyyyMMdd-HHmmss"));
            Directory.CreateDirectory(output_directory);
            File.WriteAllText(Path.Combine(output_directory, "hardware.txt"),
                $"{SystemInfo.operatingSystem}\n{SystemInfo.processorType}\n{SystemInfo.graphicsDeviceName}\nUnity {Application.unityVersion}\n256x256; native NavMesh medium avoidance; AI 10Hz; path submissions <=64/frame; damage projectiles pooled; no shadows\n");
            last_frame = Time.realtimeSinceStartupAsDouble;
            set_overview(); StartCoroutine(run_suite());
        }

        private IEnumerator run_suite()
        {
            running = true; spectator = false;
            rows.Clear();
            rows.Add("mode,width,height,setup_seconds,elapsed_seconds,frames,avg_ms,p95_ms,p99_ms,combat_frames,combat_avg_ms,combat_p95_ms,gc0,shots,hits,bites,blasts,heard_unique,ever_active,active_end,peak_active,peak_moving,dead_soldiers,dead_zombies,pending,path_failures,geometry_errors,dropped_projectiles");
            foreach (bool assault in new[] { false, true })
            {
                advancing = sampling = false;
                simulation?.Dispose(); simulation = null;
                fog?.Dispose(); fog = null;
                status = assault ? "Preparing full assault: not a sound event" : "Preparing normal 14-tile gun-noise case";
                yield return null;
                double setup_start = Time.realtimeSinceStartupAsDouble;
                simulation = new CombatStressSimulation(assault);
                fog = new CombatFog(fog_template); fog.update_visibility(simulation);
                yield return null;
                if (assault)
                {
                    for (int i = CombatStressSimulation.SOLDIERS; i < CombatStressSimulation.TOTAL; i++)
                    {
                        if (!simulation.prepare_assault_path(i)) throw new InvalidOperationException("Assault path failed: " + i);
                        if (i % 128 == 0)
                        { status = $"Preparing native paths: {i - CombatStressSimulation.SOLDIERS}/10000 (held still)"; yield return null; }
                    }
                    simulation.release_assault();
                }
                setup_seconds = Time.realtimeSinceStartupAsDouble - setup_start;
                Debug.Log($"[CombatStress] READY mode={(assault ? "assault" : "noise")} count=10400 setup_s={setup_seconds:F3}");
                started_at = Time.realtimeSinceStartupAsDouble; advancing = true;
                status = assault ? "FULL ASSAULT: all 10K released together; 60s maximum" : "NORMAL NOISE: only heard/seen zombies react; 30s";
                frames.Clear(); fighting_frames.Clear(); gc_start = GC.CollectionCount(0);
                while (Time.realtimeSinceStartupAsDouble - started_at < (assault ? 60 : 30))
                {
                    sampling = Time.realtimeSinceStartupAsDouble - started_at >= 3;
                    if (simulation.dead_soldiers == CombatStressSimulation.SOLDIERS) break;
                    yield return null;
                }
                sampling = false; save_result(assault);
            }
            advancing = false; simulation.crowd.set_paused(true);
            running = false;
            status = "DONE / paused result snapshot. R reruns | C frontline | F map | V spectator | Wheel zoom";
            Debug.Log("[CombatStress] COMPLETE " + output_directory);
        }

        private void Update()
        {
            double now = Time.realtimeSinceStartupAsDouble;
            double interval = (now - last_frame) * 1000; last_frame = now;
            frame_ms = Mathf.Lerp(frame_ms, (float)interval, .08f);
            if (sampling)
            {
                frames.Add(interval);
                if (Time.time - simulation.last_combat_time <= .25f) fighting_frames.Add(interval);
            }
            if (simulation != null)
            {
                if (advancing) simulation.step(Time.time, Time.deltaTime);
                if (fog != null && Time.time >= next_fog_update)
                { next_fog_update = Time.time + .1f; fog.update_visibility(simulation); }
                draw_scene();
                if (!spectator) fog?.draw();
            }
            if (!running && Input.GetKeyDown(KeyCode.R)) StartCoroutine(run_suite());
            if (!running && Input.GetKeyDown(KeyCode.V)) spectator = !spectator;
            if (Input.GetKeyDown(KeyCode.F)) set_overview();
            if (Input.GetKeyDown(KeyCode.C)) { Camera.main.orthographicSize = 42; Camera.main.transform.position = new Vector3(-8, 200, 0); }
            Camera camera = Camera.main;
            camera.orthographicSize = Mathf.Clamp(camera.orthographicSize - Input.mouseScrollDelta.y * 4, 8, 150);
            float x = (Input.GetKey(KeyCode.RightArrow) ? 1 : 0) - (Input.GetKey(KeyCode.LeftArrow) ? 1 : 0);
            float z = (Input.GetKey(KeyCode.UpArrow) ? 1 : 0) - (Input.GetKey(KeyCode.DownArrow) ? 1 : 0);
            camera.transform.position += new Vector3(x, 0, z) * camera.orthographicSize * Time.unscaledDeltaTime;
        }

        private void set_overview() { Camera.main.orthographicSize = 143; Camera.main.transform.position = new Vector3(0, 200, 0); }

        private void draw_scene()
        {
            Array.Clear(counts, 0, counts.Length);
            for (int i = 0; i < CombatStressSimulation.TOTAL; i++)
            {
                if (simulation.health[i] <= 0) continue;
                if (i >= 400 && !spectator && !fog.is_visible(simulation.positions[i])) continue;
                int group = i < 400 ? 0 : simulation.exploder[i] ? 2 : simulation.activated[i] ? 1 : 3;
                matrices[group][counts[group]++] = Matrix4x4.TRS(simulation.positions[i] + Vector3.up * .6f, Quaternion.identity, new Vector3(.5f, 1.2f, .5f));
            }
            foreach (var wall in simulation.crowd.walls)
            {
                int group = wall.size.y < 1 ? 8 : 4;
                matrices[group][counts[group]++] = Matrix4x4.TRS(wall.center, Quaternion.identity, wall.size);
            }
            matrices[7][counts[7]++] = Matrix4x4.TRS(new Vector3(0,-.5f,0), Quaternion.identity, new Vector3(256,1,256));
            foreach (var shot in simulation.projectiles)
                if (shot.active && (spectator || fog.is_visible(shot.position))) matrices[5][counts[5]++] = Matrix4x4.TRS(shot.position, Quaternion.identity, Vector3.one * .22f);
            foreach (var flash in simulation.flashes)
            {
                if (flash.expires <= Time.time) continue;
                if (!spectator && !fog.is_visible(flash.origin)) continue;
                float size = Mathf.Lerp(4, .2f, (flash.expires - Time.time) / .85f);
                matrices[6][counts[6]++] = Matrix4x4.TRS(flash.origin + Vector3.up * .12f, Quaternion.identity, new Vector3(size, .08f, size));
            }
            for (int group = 0; group < materials.Length; group++)
            {
                var parameters = new RenderParams(materials[group]) { worldBounds = new Bounds(Vector3.zero, new Vector3(260,20,260)), shadowCastingMode = ShadowCastingMode.Off, receiveShadows = false };
                for (int start = 0; start < counts[group]; start += 1023)
                    Graphics.RenderMeshInstanced(parameters, mesh, 0, matrices[group], Math.Min(1023, counts[group] - start), start);
            }
        }

        private void save_result(bool assault)
        {
            frames.Sort(); fighting_frames.Sort();
            var s = simulation;
            string row = string.Join(",", assault ? "assault" : "noise", Screen.width, Screen.height,
                number(setup_seconds), number(Time.realtimeSinceStartupAsDouble - started_at), frames.Count,
                number(average(frames)), number(percentile(frames, .95)), number(percentile(frames, .99)), fighting_frames.Count,
                number(average(fighting_frames)), number(percentile(fighting_frames, .95)), GC.CollectionCount(0) - gc_start,
                s.shots, s.hits, s.bites, s.blasts, s.heard, s.ever_active, s.active_now, s.peak_active, s.peak_moving,
                s.dead_soldiers, s.dead_zombies, s.pending, s.path_failures, s.geometry_errors, s.dropped_projectiles);
            rows.Add(row); File.WriteAllLines(Path.Combine(output_directory, "results.csv"), rows);
            Debug.Log("[CombatStress] RESULT " + row);
        }

        private static string number(double value) => value.ToString("F3", CultureInfo.InvariantCulture);
        private static double average(List<double> values) { double total = 0; foreach (double value in values) total += value; return values.Count == 0 ? double.NaN : total / values.Count; }
        private static double percentile(List<double> values, double fraction) => values.Count == 0 ? double.NaN : values[(int)((values.Count - 1) * fraction)];

        private void OnGUI()
        {
            GUI.matrix = Matrix4x4.Scale(Vector3.one * Mathf.Max(1, Screen.height / 800f));
            GUI.Box(new Rect(8,8,1000,155), "COMBAT STRESS | 400 SHENJI vs 9,000 RUNNERS + 1,000 EXPLODERS | 256 x 256");
            GUI.Label(new Rect(20,32,970,22), status);
            if (simulation == null) return;
            var s = simulation;
            GUI.Label(new Rect(20,55,970,22), $"Frame {frame_ms:F1}ms (~{1000 / Mathf.Max(1, frame_ms):F0} FPS) | Active {s.active_now}/10000 | Moving {s.moving_now} | Heard {s.heard} | Pending {s.pending}");
            GUI.Label(new Rect(20,78,970,22), $"Shots {s.shots} | Hits {s.hits} | Bites {s.bites} | Blasts {s.blasts} | Soldiers {400-s.dead_soldiers}/400 | Zombies {10000-s.dead_zombies}/10000");
            GUI.Label(new Rect(20,101,970,22), $"Path failures {s.path_failures} | Geometry errors {s.geometry_errors} | Dropped bullets {s.dropped_projectiles} | Gun range 7 / noise 14 | Zombie blasts SILENT");
            GUI.Label(new Rect(20,124,970,22), $"Human sight 10 / zombie sight 4 | Fog {(spectator ? "OFF (debug)" : "ON")} | C frontline / F map / V debug after test | Water/rocks block movement");
        }

        private void OnDestroy() { simulation?.Dispose(); fog?.Dispose(); foreach (var material in materials) if (material != null) Destroy(material); }
    }
}
