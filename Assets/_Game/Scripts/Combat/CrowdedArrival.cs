using UnityEngine;
using UnityEngine.AI;
using ZombieGame.Balance;

namespace ZombieGame.Combat
{
    public partial class BattleSimulation
    {
        private readonly bool[] route_settled;
        private readonly Vector3[] route_progress_goal;
        private readonly float[] route_best_distance, route_progress_at;
        private const float ARRIVAL_STALL_SECONDS = 1.2f;

        private void reset_route_progress(int index, Vector3 goal)
        {
            route_settled[index] = false;
            route_progress_goal[index] = goal;
            route_best_distance[index] = Vector3.Distance(positions[index], goal);
            route_progress_at[index] = Time.time;
        }

        /// <summary>Accept a nearby occupied destination, never a blocked corner en route.
        /// Only stationary friendly neighbours qualify; fresh commands always wake the unit.</summary>
        private bool try_settle_crowded_arrival(int index)
        {
            if (orders[index] == SoldierOrder.AttackTarget) return false;
            Vector3 goal = order_goals[index];
            float distance = Vector3.Distance(positions[index], goal);
            float progress_step = UnitBalance.config.unit_navigation_radius * .35f;
            if ((goal - route_progress_goal[index]).sqrMagnitude > .01f ||
                distance < route_best_distance[index] - progress_step)
                reset_route_progress(index, goal);
            float close_tolerance = Mathf.Max(UnitBalance.config.formation_spacing * 2,
                UnitBalance.config.unit_navigation_radius * 4);
            float tolerance = UnitBalance.config.formation_spacing * 6;
            bool crowded_inner_slot = distance > close_tolerance;
            var agent = crowd.agents[index];
            if (distance > tolerance || Time.time - route_progress_at[index] < ARRIVAL_STALL_SECONDS ||
                agent.pathPending || !agent.hasPath || agent.pathStatus != NavMeshPathStatus.PathComplete ||
                agent.remainingDistance > tolerance ||
                NavMesh.Raycast(positions[index], goal, out _, NavMesh.AllAreas) || !has_arrival_blocker(index)) return false;
            // A filled formation can strand a few agents outside their inner slots.
            // Accept its edge only after a longer stall AND a stationary friendly at the slot.
            if(crowded_inner_slot&&(Time.time-route_progress_at[index]<ARRIVAL_STALL_SECONDS*3||!is_goal_occupied(index)))return false;
            stop_soldier(index);
            route_settled[index] = true;
            // Keep AttackMove active so a newly visible enemy still triggers combat.
            if (orders[index] == SoldierOrder.Move) orders[index] = SoldierOrder.Stop;
            return true;
        }

        private bool has_arrival_blocker(int index)
        {
            Vector3 point = positions[index];
            float diameter = UnitBalance.config.unit_navigation_radius * 2;
            // Native avoidance brakes before physical contact, especially head-on.
            float reach = Mathf.Max(diameter * 2, UnitBalance.config.formation_spacing * 2);
            int min_x = CombatSpatialGrid.cell(point.x - reach), max_x = CombatSpatialGrid.cell(point.x + reach);
            int min_z = CombatSpatialGrid.cell(point.z - reach), max_z = CombatSpatialGrid.cell(point.z + reach);
            float goal_distance = (point - order_goals[index]).sqrMagnitude;
            for (int z = min_z; z <= max_z; z++)
                for (int x = min_x; x <= max_x; x++)
                    for (int other = soldier_grid.heads[x + z * CombatSpatialGrid.SIDE]; other >= 0; other = soldier_grid.next[other])
                    {
                        if (other == index || health[other] <= 0 || !crowd.agents[other].isStopped) continue;
                        if ((positions[other] - point).sqrMagnitude <= reach * reach &&
                            (positions[other] - order_goals[index]).sqrMagnitude < goal_distance &&
                            !NavMesh.Raycast(point, positions[other], out _, NavMesh.AllAreas)) return true;
                    }
            return false;
        }

        private bool is_goal_occupied(int index)
        {
            Vector3 goal=order_goals[index];float radius=UnitBalance.config.formation_spacing;
            for(int z=CombatSpatialGrid.cell(goal.z-radius);z<=CombatSpatialGrid.cell(goal.z+radius);z++)
                for(int x=CombatSpatialGrid.cell(goal.x-radius);x<=CombatSpatialGrid.cell(goal.x+radius);x++)
                    for(int other=soldier_grid.heads[x+z*CombatSpatialGrid.SIDE];other>=0;other=soldier_grid.next[other])
                        if(other!=index&&health[other]>0&&crowd.agents[other].isStopped&&
                            (positions[other]-goal).sqrMagnitude<=radius*radius)return true;
            return false;
        }
    }
}
