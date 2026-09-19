using System.Collections.Generic;
using System;
using UnityEngine;
using UnityEngine.AI;
using ZombieGame.Balance;

namespace ZombieGame.Movement
{
    /// <summary>Assigns compact destination slots, not paths. Native NavMesh still handles all routing/avoidance.</summary>
    public sealed class FormationDestinations
    {
        private readonly List<Vector3> slots = new List<Vector3>(400);
        private readonly List<int> ordered_units = new List<int>(400);
        private double[] costs, row_prices, column_prices, minimum;
        private int[] column_rows, previous_column;
        private bool[] visited;
        public double last_assignment_ms { get; private set; }

        public bool build(Vector3 center, List<Vector3> origins, List<Vector3> destinations)
        {
            var timer = System.Diagnostics.Stopwatch.StartNew();
            last_assignment_ms = 0; destinations.Clear(); slots.Clear();
            if (origins.Count == 0 || Mathf.Abs(center.x) > 127.5f || Mathf.Abs(center.z) > 127.5f
                || !NavMesh.SamplePosition(center,out var anchor,.6f,NavMesh.AllAreas)) return false;
            if (!collect_slots(anchor.position,origins.Count)) return false;
            assign_radial_bands(anchor.position,origins,destinations);
            last_assignment_ms = timer.Elapsed.TotalMilliseconds;
            return true;
        }

        private bool collect_slots(Vector3 center, int count)
        {
            float spacing = UnitBalance.config.formation_spacing;
            int initial_side = 2*Mathf.CeilToInt(Mathf.Sqrt(count/Mathf.PI))+1;
            for (int side = initial_side; side <= Mathf.CeilToInt(64 / spacing); side += 2)
            {
                slots.Clear();
                float half = (side-1)*.5f;
                for (int z = 0; z < side; z++)
                    for (int x = 0; x < side; x++)
                    {
                        Vector3 candidate = center + new Vector3((x-half)*spacing,0,(z-half)*spacing);
                        if (Mathf.Abs(candidate.x)>127.5f || Mathf.Abs(candidate.z)>127.5f) continue;
                        if (NavMesh.SamplePosition(candidate,out var hit,.1f,NavMesh.AllAreas)) slots.Add(hit.position);
                    }
                if (slots.Count < count) continue;
                slots.Sort((left,right) => (left-center).sqrMagnitude.CompareTo((right-center).sqrMagnitude));
                if (slots.Count > count) slots.RemoveRange(count,slots.Count-count);
                return true;
            }
            return false;
        }

        /// <summary>Closest units own inner rings; distance matching within a ring respects approach direction.</summary>
        private void assign_radial_bands(Vector3 center, List<Vector3> origins, List<Vector3> destinations)
        {
            ordered_units.Clear();
            for (int i=0;i<origins.Count;i++) { ordered_units.Add(i); destinations.Add(Vector3.zero); }
            ordered_units.Sort((left,right) => (origins[left]-center).sqrMagnitude.CompareTo((origins[right]-center).sqrMagnitude));
            int first = 0;
            while (first < slots.Count)
            {
                int ring = radial_band(slots[first],center), end = first+1;
                while (end < slots.Count && radial_band(slots[end],center) == ring) end++;
                assign_minimum_cost(origins,destinations,first,end-first);
                first = end;
            }
        }

        private static int radial_band(Vector3 point, Vector3 center) => Mathf.FloorToInt(Vector3.Distance(point,center)/UnitBalance.config.formation_spacing + .001f);

        /// <summary>Minimum-squared-distance matching within one annulus; this assigns endpoints, never paths.</summary>
        private void assign_minimum_cost(List<Vector3> origins, List<Vector3> destinations, int first, int count)
        {
            ensure_buffers(count);
            for (int row=0;row<count;row++)
            {
                for (int column=0;column<count;column++) costs[row*count+column] = (origins[ordered_units[first+row]]-slots[first+column]).sqrMagnitude;
            }
            for (int row=1;row<=count;row++)
            {
                column_rows[0] = row; int column = 0;
                for (int i=0;i<=count;i++) { minimum[i]=double.PositiveInfinity; visited[i]=false; }
                do
                {
                    visited[column] = true;
                    int current_row = column_rows[column], next_column = 0;
                    double delta = double.PositiveInfinity;
                    for (int candidate=1;candidate<=count;candidate++)
                    {
                        if (visited[candidate]) continue;
                        double reduced = costs[(current_row-1)*count+candidate-1]-row_prices[current_row]-column_prices[candidate];
                        if (reduced < minimum[candidate]) { minimum[candidate]=reduced; previous_column[candidate]=column; }
                        if (minimum[candidate] < delta) { delta=minimum[candidate]; next_column=candidate; }
                    }
                    for (int i=0;i<=count;i++)
                        if (visited[i]) { row_prices[column_rows[i]]+=delta; column_prices[i]-=delta; }
                        else minimum[i]-=delta;
                    column = next_column;
                } while (column_rows[column] != 0);
                do
                {
                    int previous = previous_column[column];
                    column_rows[column] = column_rows[previous]; column = previous;
                } while (column != 0);
            }
            for (int column=1;column<=count;column++) destinations[ordered_units[first+column_rows[column]-1]] = slots[first+column-1];
        }

        private void ensure_buffers(int count)
        {
            if (costs == null || costs.Length < count*count)
            {
                costs = new double[count*count]; row_prices = new double[count+1]; column_prices = new double[count+1];
                minimum = new double[count+1]; column_rows = new int[count+1]; previous_column = new int[count+1]; visited = new bool[count+1];
            }
            Array.Clear(row_prices,0,row_prices.Length); Array.Clear(column_prices,0,column_prices.Length);
            Array.Clear(column_rows,0,column_rows.Length);
        }
    }
}
