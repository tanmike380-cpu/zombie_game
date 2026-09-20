using System;
using ZombieGame.Balance;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace ZombieGame.Navigation
{
    /// <summary>Reusable native agents and box-based map bake. All routing/avoidance belongs to Unity.</summary>
    public class NativeNavMeshCrowd : IDisposable
    {
        public readonly NavMeshAgent[] agents;
        public readonly Transform[] transforms;
        public Bounds[] walls {get;private set;}
        public double setup_ms { get; protected set; }
        private readonly GameObject root;
        private readonly NavMeshData nav_data;
        private readonly NavMeshDataInstance nav_instance;
        private readonly Vector3[] spawn_positions;
        private readonly UnitStats stats;

        public NativeNavMeshCrowd(Vector3[] spawn_positions, Bounds[] obstacles, UnitStats stats = null)
        {
            if (spawn_positions == null || obstacles == null) throw new ArgumentException("Navigation requires explicit spawns and map obstacles");
            int count = spawn_positions.Length;
            this.spawn_positions = spawn_positions;
            this.stats = stats ?? UnitBalance.runner;
            var timer = System.Diagnostics.Stopwatch.StartNew();
            walls = obstacles;
            var sources = new List<NavMeshBuildSource>();
            sources.Add(box_source(new Bounds(new Vector3(0, -.5f, 0), new Vector3(256, 1, 256)), 0));
            foreach (Bounds wall in walls) sources.Add(box_source(wall, 1));
            NavMeshBuildSettings settings = NavMesh.GetSettingsByIndex(0);
            settings.agentRadius = UnitBalance.config.unit_navigation_radius;
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
            unit.transform.position = spawn_positions[index];
            var agent = unit.AddComponent<NavMeshAgent>();
            agent.agentTypeID = agent_type;
            agent.radius = UnitBalance.config.unit_navigation_radius;
            agent.height = 1.2f;
            agent.speed = stats.move_speed;
            agent.acceleration = stats.acceleration;
            agent.angularSpeed = 720;
            agent.stoppingDistance = .25f;
            agent.obstacleAvoidanceType = ObstacleAvoidanceType.MedQualityObstacleAvoidance;
            agent.avoidancePriority = 30 + index % 40;
            transforms[index] = unit.transform;
            agents[index] = agent;
            if (!agent.isOnNavMesh) throw new InvalidOperationException("Agent spawn is off NavMesh: " + index);
            agent.isStopped = true;
            agent.enabled = false;
        }

        /// <summary>Issue a native route only after a stimulus. Idle agents have no navigation workload.</summary>
        public bool investigate_position(int index, Vector3 target)
        {
            NavMeshAgent agent = agents[index];
            agent.enabled = true;
            if (!agent.isOnNavMesh) return false;
            var path = new NavMeshPath();
            if (!NavMesh.CalculatePath(transforms[index].position, target, NavMesh.AllAreas, path)
                || path.status != NavMeshPathStatus.PathComplete || !agent.SetPath(path)) return false;
            agent.stoppingDistance = .8f;
            agent.isStopped = false;
            return true;
        }

        public void set_paused(bool paused)
        {
            for (int i = 0; i < agents.Length; i++)
                if (agents[i].enabled && agents[i].isOnNavMesh) agents[i].isStopped = paused;
        }

        /// <summary>Unity-owned carving for runtime construction; no custom routing.</summary>
        public GameObject add_building(Bounds bounds)
        {
            var obstacle=new GameObject("Constructed building navigation");obstacle.transform.SetParent(root.transform,false);
            obstacle.transform.position=bounds.center;
            var carving=obstacle.AddComponent<NavMeshObstacle>();carving.shape=NavMeshObstacleShape.Box;
            carving.size=bounds.size;carving.carving=true;carving.carveOnlyStationary=false;
            var updated=new List<Bounds>(walls){bounds};walls=updated.ToArray();return obstacle;
        }
        public void remove_building(Bounds bounds,GameObject obstacle)
        {
            if(obstacle==null)return;
            obstacle.SetActive(false);UnityEngine.Object.Destroy(obstacle);
            var updated=new List<Bounds>(walls);updated.Remove(bounds);walls=updated.ToArray();
        }
        public void Dispose()
        {
            // Unity may destroy the hierarchy before the controller on exiting Play Mode.
            if (root != null) { root.SetActive(false); UnityEngine.Object.Destroy(root); }
            if (nav_instance.valid) nav_instance.Remove();
            if (nav_data != null) UnityEngine.Object.Destroy(nav_data);
        }
    }
}
