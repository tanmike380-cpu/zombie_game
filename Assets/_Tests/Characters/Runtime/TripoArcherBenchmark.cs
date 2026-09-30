using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.Profiling;
using ZombieGame.Combat;
using ZombieGame.Presentation;

namespace ZombieGame.CharacterTests
{
    /// <summary>Reproducible fixtures, no unit balance overrides. Reports rendering separately from actual simulation.</summary>
    public sealed class TripoArcherBenchmark : MonoBehaviour
    {
        public TripoArcherBattle game;
        private readonly List<StageResult> results = new List<StageResult>();
        private string output_directory;
        private double setup_seconds;
        private double command_seconds;

        [Serializable]
        public sealed class StageResult
        {
            public string name;
            public int humans, zombies, frame_count, width, height, submitted, culled, shots, hits, kills, moving, dropped_projectiles;
            public double duration_seconds, mean_ms, p95_ms, p99_ms, fps, setup_seconds, command_seconds;
            public long allocated_mb;
        }

        [Serializable]
        private sealed class Report
        {
            public string hardware, unity, conditions;
            public int model_triangles;
            public StageResult[] stages;
        }

        public void begin()
        {
            if (game.benchmarking) return;
            initialize_output();
            StartCoroutine(run_suite());
        }

        public void begin_smoke()
        {
            initialize_output();
            StartCoroutine(run_smoke());
        }

        private void initialize_output()
        {
            string[] args = Environment.GetCommandLineArgs();
            int output_index = Array.IndexOf(args, "-tripoOutput");
            output_directory = output_index >= 0 && output_index + 1 < args.Length ? args[output_index + 1]
                : Path.Combine(Application.persistentDataPath, "TripoArcher", DateTime.Now.ToString("yyyyMMdd-HHmmss"));
            Directory.CreateDirectory(output_directory);
        }

        private IEnumerator run_suite()
        {
            game.benchmarking = true;
            game.status = "Preparing 10,000 full-detail Tripo archers";
            yield return null;
            double start = Time.realtimeSinceStartupAsDouble;
            game.reset_population(10000, 0, true);
            setup_seconds = Time.realtimeSinceStartupAsDouble - start;
            if (game.original_rest_only || Array.IndexOf(Environment.GetCommandLineArgs(), "-tripoRestOnly") >= 0)
            {
                game.forced_pose = CharacterPose.Idle;
                game.frustum_culling = false;
                yield return sample("original_10000_overview_NO_ANIMATION", 12);
                Camera.main.orthographicSize = 14;
                yield return sample("original_10000_zoom_only_NO_ANIMATION", 12);
                game.frustum_culling = true;
                yield return sample("original_10000_zoom_and_cull_NO_ANIMATION", 12);
                write_report();
                game.status = "Original-model baseline complete. Animation is awaiting review.";
                game.benchmarking = false;
                if (Array.IndexOf(Environment.GetCommandLineArgs(), "-tripoQuit") >= 0) Application.Quit();
                yield break;
            }
            foreach (var pose in new[] { CharacterPose.Idle, CharacterPose.Run, CharacterPose.Attack })
            {
                game.forced_pose = pose;
                yield return sample("render_10000_" + pose, 10);
            }
            game.status = "Preparing native NavMesh + 10,000 agents (setup measured separately)";
            yield return null;
            start = Time.realtimeSinceStartupAsDouble;
            game.reset_population(10000, 0, false);
            setup_seconds = Time.realtimeSinceStartupAsDouble - start;
            start = Time.realtimeSinceStartupAsDouble;
            // Uniform filled-grid march fixture, all commands in the same simulation frame.
            for (int i = 0; i < game.current.soldier_count; i++)
                if (!game.current.issue_order(i, SoldierOrder.Move, game.current.positions[i] + Vector3.forward * 50))
                    throw new InvalidOperationException("10K fixture route rejected: " + i);
            command_seconds = Time.realtimeSinceStartupAsDouble - start;
            yield return sample("navigation_10000_actual", 10);
            game.status = "Preparing 10,000 archers versus 500 zombies";
            yield return null;
            start = Time.realtimeSinceStartupAsDouble;
            game.reset_population(10000, 500, false);
            setup_seconds = Time.realtimeSinceStartupAsDouble - start;
            start = Time.realtimeSinceStartupAsDouble;
            for (int i = 0; i < game.current.soldier_count; i++)
                game.current.issue_order(i, SoldierOrder.AttackMove, game.current.positions[i] + Vector3.forward * 40);
            command_seconds = Time.realtimeSinceStartupAsDouble - start;
            yield return sample("battle_10000_vs_500_actual", 16);
            write_report();
            game.status = "10K test complete; report saved. R returns to the small battle fixture.";
            game.benchmarking = false;
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-tripoQuit") >= 0) Application.Quit();
        }

        private IEnumerator sample(string name, float seconds)
        {
            game.status = name;
            yield return new WaitForSecondsRealtime(2);
            var samples = new List<double>();
            double started = Time.realtimeSinceStartupAsDouble;
            double previous = started;
            int peak_moving = 0;
            while (Time.realtimeSinceStartupAsDouble - started < seconds)
            {
                yield return null;
                double now = Time.realtimeSinceStartupAsDouble;
                samples.Add((now - previous) * 1000);
                previous = now;
                if (game.current != null)
                {
                    int moving = 0;
                    foreach (var agent in game.current.crowd.agents)
                        if (agent.enabled && agent.velocity.sqrMagnitude > .04f) moving++;
                    peak_moving = Math.Max(peak_moving, moving);
                }
            }
            samples.Sort();
            double mean = samples.Average();
            var result = new StageResult { name = name, humans = game.visual_count, zombies = game.current?.zombie_count ?? 0,
                frame_count = samples.Count, width = Screen.width, height = Screen.height,
                submitted = game.crowd_renderer.submitted, culled = game.crowd_renderer.culled, duration_seconds = previous - started,
                mean_ms = mean, fps = 1000 / mean, p95_ms = samples[(int)((samples.Count - 1) * .95)],
                p99_ms = samples[(int)((samples.Count - 1) * .99)], setup_seconds = setup_seconds, command_seconds = command_seconds,
                allocated_mb = Profiler.GetTotalAllocatedMemoryLong() / 1048576,
                shots = game.current?.shots ?? 0, hits = game.current?.hits ?? 0, kills = game.current?.dead_zombies ?? 0,
                moving = peak_moving, dropped_projectiles = game.current?.dropped_projectiles ?? 0 };
            results.Add(result);
            Debug.Log("[TripoBenchmark] " + JsonUtility.ToJson(result));
            ScreenCapture.CaptureScreenshot(Path.Combine(output_directory, name + ".png"));
            yield return new WaitForEndOfFrame();
            write_report();
        }

        private IEnumerator run_smoke()
        {
            game.benchmarking = true;
            yield return new WaitForSecondsRealtime(2);
            ScreenCapture.CaptureScreenshot(Path.Combine(output_directory, "combat-close.png"));
            yield return new WaitForSecondsRealtime(15);
            var battle = game.current;
            bool passed = battle.shots > 0 && battle.hits > 0 && battle.dead_zombies >= 3 && battle.dropped_projectiles == 0;
            string message = $"{(passed ? "PASS" : "FAIL")} shots={battle.shots} hits={battle.hits} kills={battle.dead_zombies} alive={battle.living_soldiers} dropped={battle.dropped_projectiles}";
            File.WriteAllText(Path.Combine(output_directory, "combat-smoke.txt"), message);
            Debug.Log("[TripoArcherSmoke] " + message);
            ScreenCapture.CaptureScreenshot(Path.Combine(output_directory, "combat-result.png"));
            yield return new WaitForSecondsRealtime(1);
            game.benchmarking = false;
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-tripoQuit") >= 0) Application.Quit(passed ? 0 : 2);
        }

        private void write_report()
        {
            var report = new Report { hardware = $"{SystemInfo.processorType}; {SystemInfo.graphicsDeviceName}; RAM {SystemInfo.systemMemorySize} MB; {SystemInfo.operatingSystem}",
                unity = Application.unityVersion, model_triangles = game.archer_frames.poses[0].frames[0].triangles.Length / 3,
                conditions = "Standalone Metal; original 2K texture; full mesh, no LOD; shared mesh instancing, not 10K Animators; all-army overview; MSAA2; VSync off; no character shadows. NO_ANIMATION stage uses untouched original FBX rest geometry and excludes all animation/AI/navigation. Other render stages use authored animation, exclude AI/nav. Actual stages include production BattleSimulation, native avoidance, fog updates, HP/ammo UI and projectiles. Two-second warmup. Real-time frame intervals include benchmark instrumentation. Scene setup and all-at-once command dispatch excluded from frame averages and reported separately.",
                stages = results.ToArray() };
            File.WriteAllText(Path.Combine(output_directory, "report.json"), JsonUtility.ToJson(report, true));
        }
    }
}
