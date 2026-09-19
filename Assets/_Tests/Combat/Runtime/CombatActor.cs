using UnityEngine;
using ZombieGame.Balance;
using UnityEngine.AI;

namespace ZombieGame.CombatTests
{
    public enum CombatOrder { Stop, Move, AttackTarget, AttackMove, Patrol }

    public sealed class CombatActor : MonoBehaviour
    {
        public bool friendly;
        public bool exploder;
        public float detonate_at = -1;
        public LineRenderer blast_ring;
        public float health;
        public float max_health;
        public NavMeshAgent agent;
        public CombatOrder order;
        public CombatActor target;
        public Vector3 destination;
        public Vector3 patrol_start;
        public Vector3 patrol_end;
        public Vector3 memory_position;
        public bool has_memory;
        [System.NonSerialized] public ZombieGame.AI.SoundMemory sound_memory;
        public bool selected;
        public float next_attack;
        public float next_repath;
        public Vector3 last_path_goal = Vector3.positiveInfinity;
        public LineRenderer selection_ring;
        private readonly NavMeshPath path = new NavMeshPath();
        public bool alive => health > 0;
        public UnitStats stats => friendly ? UnitBalance.human : exploder ? UnitBalance.exploder : UnitBalance.runner;

        public void halt()
        {
            if (agent.enabled && agent.isOnNavMesh)
            { agent.isStopped = true; agent.ResetPath(); agent.velocity = Vector3.zero; }
            last_path_goal = Vector3.positiveInfinity;
            next_repath = 0;
        }

        public void walk_to(Vector3 goal, float stopping_distance)
        {
            if (!alive || !agent.enabled || !agent.isOnNavMesh) return;
            agent.stoppingDistance = stopping_distance;
            agent.isStopped = false;
            if (Time.time < next_repath && Vector3.Distance(goal, last_path_goal) < .5f) return;
            if (Vector3.Distance(goal, last_path_goal) < .15f && agent.hasPath) return;
            next_repath = Time.time + UnitBalance.config.chase_repath_seconds;
            last_path_goal = goal;
            // Small combat sandbox: resolve explicit paths now, without the async request queue.
            if (agent.CalculatePath(goal, path) && path.status == NavMeshPathStatus.PathComplete)
                agent.SetPath(path);
            else halt();
        }

        private void LateUpdate()
        {
            if (!alive || !agent.enabled || agent.isStopped) return;
            Vector3 direction = agent.desiredVelocity;
            direction.y = 0;
            if (direction.sqrMagnitude > .01f) transform.rotation = Quaternion.LookRotation(direction);
        }
    }
}
