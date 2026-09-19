using System;
using UnityEngine;
using UnityEngine.AI;
using ZombieGame.Balance;
using ZombieGame.Navigation;

namespace ZombieGame.PerformanceTests
{
    /// <summary>Legacy benchmark layout/counters only; agent navigation lives in _Game.</summary>
    public sealed class NavMeshCrowd : NativeNavMeshCrowd
    {
        public readonly bool[] arrived;
        private readonly bool[] geometry_failed;
        public int arrived_count, pending_count, invalid_paths, geometry_errors, ready_count;
        public NavMeshCrowd(int count, bool obstacles, Vector3[] spawn_positions = null,
            Bounds[] custom_walls = null, bool prepare_paths = true, UnitStats stats = null)
            : base(spawn_positions ?? create_spawns(count),custom_walls ?? create_walls(obstacles),stats)
        {
            arrived = new bool[count]; geometry_failed = new bool[count];
            if (!prepare_paths) return;
            var timer = System.Diagnostics.Stopwatch.StartNew();
            for (int i=0;i<count;i++)
            {
                Vector3 goal = new Vector3(110+i%16*.5f,0,-110+i/16%400*.55f);
                if (!investigate_position(i,goal)) throw new InvalidOperationException("Benchmark route unavailable: "+i);
                agents[i].stoppingDistance=.25f; agents[i].isStopped=true;
            }
            timer.Stop(); setup_ms += timer.Elapsed.TotalMilliseconds;
        }
        private static Vector3[] create_spawns(int count)
        {
            var positions = new Vector3[count];
            for (int i=0;i<count;i++) positions[i] = new Vector3(-123.5f+i%64,0,-119.5f+i/64);
            return positions;
        }
        private static Bounds[] create_walls(bool obstacles) => obstacles ? new[] {
            new Bounds(new Vector3(-47,1.5f,-25.5f),new Vector3(2,3,205)),
            new Bounds(new Vector3(18,1.5f,25),new Vector3(2,3,206)),
            new Bounds(new Vector3(78,1.5f,-68),new Vector3(2,3,120)),
            new Bounds(new Vector3(78,1.5f,66),new Vector3(2,3,124))
        } : Array.Empty<Bounds>();
        public void update_counters(bool allow_arrival)
        {
            pending_count = invalid_paths = ready_count = 0;
            for (int i = 0; i < agents.Length; i++)
            {
                if (arrived[i]) continue;
                NavMeshAgent agent = agents[i];
                Vector3 position = transforms[i].position;
                if (!geometry_failed[i] && is_invalid_position(position))
                {
                    geometry_failed[i] = true;
                    geometry_errors++;
                }
                if (!agent.isOnNavMesh) { invalid_paths++; continue; }
                if (agent.pathPending) { pending_count++; continue; }
                if (!agent.hasPath || agent.pathStatus != NavMeshPathStatus.PathComplete) { invalid_paths++; continue; }
                ready_count++;
                if (allow_arrival && position.x >= 96)
                {
                    arrived[i] = true;
                    arrived_count++;
                    agent.enabled = false;
                }
            }
        }

        private bool is_invalid_position(Vector3 position)
        {
            if (float.IsNaN(position.x) || float.IsNaN(position.z) || Mathf.Abs(position.x) > 128 || Mathf.Abs(position.z) > 128) return true;
            foreach (Bounds wall in walls)
                if (position.x > wall.min.x && position.x < wall.max.x && position.z > wall.min.z && position.z < wall.max.z) return true;
            return false;
        }


    }
}
