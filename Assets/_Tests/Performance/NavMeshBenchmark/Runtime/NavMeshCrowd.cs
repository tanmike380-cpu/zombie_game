using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace ZombieGame.PerformanceTests
{
    /// <summary>Test fixture only. All routing, steering and avoidance are Unity's native implementation.</summary>
    public sealed class NavMeshCrowd : IDisposable
    {
        public readonly NavMeshAgent[] agents;
        public readonly Transform[] transforms;
        public readonly bool[] arrived;
        public readonly Bounds[] walls;
        public int arrived_count;
        public int pending_count;
        public int invalid_paths;
        public int geometry_errors;
        public int ready_count;
        public readonly double setup_ms;
        private readonly GameObject root;
        private readonly NavMeshData nav_data;
        private readonly NavMeshDataInstance nav_instance;
        private readonly bool[] geometry_failed;

        public NavMeshCrowd(int count, bool obstacles)
        {
            var timer = System.Diagnostics.Stopwatch.StartNew();
            walls = obstacles ? new[] {
                new Bounds(new Vector3(-47, 1.5f, -25.5f), new Vector3(2, 3, 205)),
                new Bounds(new Vector3(18, 1.5f, 25), new Vector3(2, 3, 206)),
                new Bounds(new Vector3(78, 1.5f, -68), new Vector3(2, 3, 120)),
                new Bounds(new Vector3(78, 1.5f, 66), new Vector3(2, 3, 124))
            } : Array.Empty<Bounds>();
            var sources = new List<NavMeshBuildSource>();
            sources.Add(box_source(new Bounds(new Vector3(0, -.5f, 0), new Vector3(256, 1, 256)), 0));
            foreach (Bounds wall in walls) sources.Add(box_source(wall, 1));
            NavMeshBuildSettings settings = NavMesh.GetSettingsByIndex(0);
            settings.agentRadius = .25f;
            settings.agentHeight = 1.2f;
            settings.agentClimb = .2f;
            settings.overrideVoxelSize = true;
            settings.voxelSize = .1f;
            nav_data = NavMeshBuilder.BuildNavMeshData(settings, sources,
                new Bounds(Vector3.zero, new Vector3(260, 10, 260)), Vector3.zero, Quaternion.identity);
            if (nav_data == null) throw new InvalidOperationException("Unity NavMesh build returned null");
            nav_instance = NavMesh.AddNavMeshData(nav_data);
            root = new GameObject("Native NavMesh Agents");
            agents = new NavMeshAgent[count];
            transforms = new Transform[count];
            arrived = new bool[count];
            geometry_failed = new bool[count];
            for (int i = 0; i < count; i++) spawn_agent(i, settings.agentTypeID);
            timer.Stop();
            setup_ms = timer.Elapsed.TotalMilliseconds;
        }

        private static NavMeshBuildSource box_source(Bounds bounds, int area)
        {
            return new NavMeshBuildSource { shape = NavMeshBuildSourceShape.Box,
                transform = Matrix4x4.TRS(bounds.center, Quaternion.identity, Vector3.one), size = bounds.size, area = area };
        }

        private void spawn_agent(int index, int agent_type)
        {
            var unit = new GameObject("Agent");
            unit.transform.SetParent(root.transform, false);
            unit.transform.position = new Vector3(-123.5f + index % 64, 0, -119.5f + index / 64);
            var agent = unit.AddComponent<NavMeshAgent>();
            agent.agentTypeID = agent_type;
            agent.radius = .25f;
            agent.height = 1.2f;
            agent.speed = 12;
            agent.acceleration = 40;
            agent.angularSpeed = 720;
            agent.stoppingDistance = .25f;
            agent.obstacleAvoidanceType = ObstacleAvoidanceType.MedQualityObstacleAvoidance;
            agent.avoidancePriority = 30 + index % 40;
            transforms[index] = unit.transform;
            agents[index] = agent;
            if (!agent.isOnNavMesh) throw new InvalidOperationException("Agent spawn is off NavMesh: " + index);
            agent.isStopped = true;
            // Spread goals in the exit region; remove arrivals from avoidance to avoid an impossible single-point pile-up.
            Vector3 goal = new Vector3(110 + index % 16 * .5f, 0, -110 + index / 16 % 400 * .55f);
            // Pure movement test: calculate with Unity's native API before releasing ANY unit.
            // Setup cost is measured separately, not hidden as a realistic sound-response result.
            var path = new NavMeshPath();
            if (!NavMesh.CalculatePath(unit.transform.position, goal, NavMesh.AllAreas, path)
                || path.status != NavMeshPathStatus.PathComplete || !agent.SetPath(path))
                throw new InvalidOperationException("Native complete path unavailable: " + index);
            agent.isStopped = true;
        }

        public void set_paused(bool paused)
        {
            for (int i = 0; i < agents.Length; i++)
                if (agents[i].enabled && agents[i].isOnNavMesh) agents[i].isStopped = paused;
        }

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

        public void Dispose()
        {
            root.SetActive(false);
            UnityEngine.Object.Destroy(root);
            nav_instance.Remove();
            UnityEngine.Object.Destroy(nav_data);
        }
    }
}
