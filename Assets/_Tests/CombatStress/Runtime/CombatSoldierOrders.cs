using System;
using ZombieGame.Balance;
using UnityEngine;
using UnityEngine.AI;

namespace ZombieGame.CombatStressTests
{
    public enum SoldierOrder { Stop, Move, AttackMove, AttackTarget, Patrol }

    public sealed partial class CombatStressSimulation
    {
        public readonly bool[] selected = new bool[SOLDIERS];
        public readonly SoldierOrder[] orders = new SoldierOrder[SOLDIERS];
        public readonly int[] order_targets = new int[SOLDIERS];
        public readonly Vector3[] order_goals = new Vector3[SOLDIERS];
        private readonly Vector3[] patrol_starts = new Vector3[SOLDIERS], patrol_ends = new Vector3[SOLDIERS];
        private readonly Vector3[] last_soldier_goal = new Vector3[SOLDIERS];
        private readonly float[] soldier_repath_at = new float[SOLDIERS], zombie_repath_at = new float[TOTAL];
        private readonly NavMeshPath soldier_path = new NavMeshPath();
        public Func<Vector3, bool> player_visibility;

        public bool issue_order(int index, SoldierOrder order, Vector3 goal, int target = -1)
        {
            if (!playable || index < 0 || index >= SOLDIERS || health[index] <= 0) return false;
            if (order == SoldierOrder.AttackTarget)
            {
                if (target < SOLDIERS || target >= TOTAL || health[target] <= 0 ||
                    (player_visibility != null && !player_visibility(positions[target]))) return false;
                goal = positions[target];
            }
            if (order != SoldierOrder.Stop)
            {
                if (Mathf.Abs(goal.x) > 127.5f || Mathf.Abs(goal.z) > 127.5f ||
                    !NavMesh.SamplePosition(goal, out var hit, .6f, NavMesh.AllAreas)) return false;
                goal = hit.position;
            }
            stop_soldier(index);
            orders[index] = order; order_targets[index] = target; order_goals[index] = goal;
            patrol_starts[index] = positions[index]; patrol_ends[index] = goal;
            soldier_repath_at[index] = 0;
            if (order == SoldierOrder.Stop) return true;
            if (order == SoldierOrder.AttackTarget && (goal - positions[index]).sqrMagnitude <= UnitBalance.human.attack_range * UnitBalance.human.attack_range && visible(positions[index], goal)) return true;
            if (walk_soldier(index, goal, .15f, true)) return true;
            orders[index] = SoldierOrder.Stop; return false;
        }

        private void stop_soldier(int index)
        {
            var agent = crowd.agents[index];
            if (agent.enabled && agent.isOnNavMesh)
            { agent.isStopped = true; agent.ResetPath(); agent.velocity = Vector3.zero; }
            last_soldier_goal[index] = Vector3.positiveInfinity;
        }

        private bool walk_soldier(int index, Vector3 goal, float stopping_distance, bool immediate = false)
        {
            var agent = crowd.agents[index];
            if (!agent.enabled || !agent.isOnNavMesh) return false;
            agent.stoppingDistance = stopping_distance;
            if (!immediate && Time.time < soldier_repath_at[index]) return true;
            if (!immediate && agent.hasPath && (last_soldier_goal[index] - goal).sqrMagnitude < .25f)
            { agent.isStopped = false; return true; }
            soldier_repath_at[index] = Time.time + .25f;
            if (!agent.CalculatePath(goal, soldier_path) || soldier_path.status != NavMeshPathStatus.PathComplete || !agent.SetPath(soldier_path))
            { stop_soldier(index); return false; }
            last_soldier_goal[index] = goal; agent.isStopped = false; return true;
        }

        /// <summary>Returns an in-range firing target; move orders never auto-fire.</summary>
        private int update_player_order(int index, float now)
        {
            SoldierOrder order = orders[index];
            if (order == SoldierOrder.Move) { follow_soldier_route(index); return -1; }
            int target;
            if (order == SoldierOrder.AttackTarget)
            {
                target = order_targets[index];
                if (target < SOLDIERS || health[target] <= 0)
                { orders[index] = SoldierOrder.Stop; stop_soldier(index); return -1; }
                if (player_visibility != null && !player_visibility(positions[target]))
                { follow_soldier_route(index); return -1; }
                order_goals[index] = positions[target];
            }
            else target = nearest(zombie_grid, positions[index], order == SoldierOrder.Stop ? UnitBalance.human.attack_range : UnitBalance.config.human_sight);
            if (target >= 0)
            {
                bool line_clear = visible(positions[index], positions[target]);
                if ((positions[index] - positions[target]).sqrMagnitude <= UnitBalance.human.attack_range * UnitBalance.human.attack_range && line_clear)
                { stop_soldier(index); return target; }
                if (order != SoldierOrder.Stop)
                { walk_soldier(index, positions[target], line_clear ? UnitBalance.human.attack_range - .5f : .15f); return -1; }
            }
            follow_soldier_route(index); return -1;
        }

        private void follow_soldier_route(int index)
        {
            if (orders[index] == SoldierOrder.Stop) { stop_soldier(index); return; }
            if ((positions[index] - order_goals[index]).sqrMagnitude < .35f * .35f)
            {
                if (orders[index] == SoldierOrder.Patrol)
                    order_goals[index] = (order_goals[index] - patrol_ends[index]).sqrMagnitude < .1f
                        ? patrol_starts[index] : patrol_ends[index];
                else { orders[index] = SoldierOrder.Stop; stop_soldier(index); return; }
            }
            walk_soldier(index, order_goals[index], .15f);
        }
    }
}
