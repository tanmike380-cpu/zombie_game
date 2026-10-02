using System;
using ZombieGame.Balance;
using UnityEngine;
using UnityEngine.AI;

namespace ZombieGame.Combat
{
    public enum SoldierOrder { Stop, Move, AttackMove, AttackTarget, Patrol }

    public partial class BattleSimulation
    {
        public readonly bool[] selected;
        public readonly SoldierOrder[] orders;
        public readonly int[] order_targets;
        public readonly Vector3[] order_goals;
        private readonly Vector3[] patrol_starts, patrol_ends;
        private readonly Vector3[] last_soldier_goal;
        private readonly float[] soldier_repath_at, zombie_repath_at;
        private readonly NavMeshPath soldier_path = new NavMeshPath();
        public Func<Vector3, bool> player_visibility;

        public bool issue_order(int index, SoldierOrder order, Vector3 goal, int target = -1)
        {
            if (!playable || index < 0 || index >= soldier_count || health[index] <= 0) return false;
            if (order == SoldierOrder.AttackTarget)
            {
                if (target < soldier_count || target >= total_count || health[target] <= 0 ||
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
            reset_route_progress(index, goal);
            orders[index] = order; order_targets[index] = target; order_goals[index] = goal;
            patrol_starts[index] = positions[index]; patrol_ends[index] = goal;
            soldier_repath_at[index] = 0;
            if (order == SoldierOrder.Stop) return true;
            if (order == SoldierOrder.AttackTarget && (goal - positions[index]).sqrMagnitude <= Mathf.Pow(human_target_range(index,target),2) && human_line_clear(index, goal)) return true;
            if (walk_soldier(index, goal, .15f, true)) return true;
            orders[index] = SoldierOrder.Stop; return false;
        }

        private void stop_soldier(int index)
        {
            var agent = crowd.agents[index];
            if (agent.enabled && agent.isOnNavMesh)
            { agent.ResetPath(); agent.isStopped = true; agent.velocity = Vector3.zero; }
            last_soldier_goal[index] = Vector3.positiveInfinity;
        }

        private bool walk_soldier(int index, Vector3 goal, float stopping_distance, bool immediate = false)
        {
            var agent = crowd.agents[index];
            if (!agent.enabled || !agent.isOnNavMesh) return false;
            route_settled[index] = false;
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
            if (order == SoldierOrder.Move)
            {
                follow_soldier_route(index);
                return stats_for(index).id=="greek_fire"?nearest(zombie_grid,positions[index],human_range(index),true):-1;
            }
            int target;
            if (order == SoldierOrder.AttackTarget)
            {
                target = order_targets[index];
                if (target < soldier_count || health[target] <= 0)
                { orders[index] = SoldierOrder.Stop; stop_soldier(index); return -1; }
                if (player_visibility != null && !player_visibility(positions[target]))
                { follow_soldier_route(index); return -1; }
                order_goals[index] = positions[target];
            }
            else target = nearest(zombie_grid, positions[index], order == SoldierOrder.Stop&&!uses_melee(index) ? human_range(index) : UnitBalance.config.human_sight, !uses_melee(index));
            if (target >= 0)
            {
                bool line_clear = human_line_clear(index, positions[target]);
                float target_range = human_target_range(index,target);
                if ((positions[index] - positions[target]).sqrMagnitude <= target_range * target_range && line_clear)
                { stop_soldier(index); return target; }
                if (order != SoldierOrder.Stop||uses_melee(index))
                { walk_soldier(index, positions[target], line_clear ? Mathf.Max(contact_distance(index,target),target_range-.05f) : .15f); return -1; }
            }
            follow_soldier_route(index); return -1;
        }

        private void follow_soldier_route(int index)
        {
            if (orders[index] == SoldierOrder.Stop) { stop_soldier(index); return; }
            if (route_settled[index])
            {
                // Patrol keeps its endpoint and resumes when the local obstruction clears.
                // Native avoidance may displace an already-settled soldier; do not freeze it outside the accepted band.
                float settled_radius=UnitBalance.config.formation_spacing*6+.05f;
                bool still_near=(positions[index]-order_goals[index]).sqrMagnitude<=settled_radius*settled_radius;
                if (still_near&&(orders[index] != SoldierOrder.Patrol || has_arrival_blocker(index))) return;
                reset_route_progress(index, order_goals[index]);
            }
            if (try_settle_crowded_arrival(index)) return;
            // Slot tolerance must not grow with body radius: stopping a diameter short leaves gaps.
            float arrival_radius = .25f;
            if ((positions[index] - order_goals[index]).sqrMagnitude < arrival_radius * arrival_radius)
            {
                if (orders[index] == SoldierOrder.Patrol)
                    order_goals[index] = (order_goals[index] - patrol_ends[index]).sqrMagnitude < .1f
                        ? patrol_starts[index] : patrol_ends[index];
                else
                {
                    // Arrival is not an explicit Stop command: retain attack-move and its recovery goal.
                    stop_soldier(index);
                    route_settled[index] = true;
                    return;
                }
            }
            walk_soldier(index, order_goals[index], .15f);
        }
    }
}
