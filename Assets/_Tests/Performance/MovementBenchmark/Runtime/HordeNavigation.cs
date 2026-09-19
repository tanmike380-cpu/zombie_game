using System;
using UnityEngine;

namespace ZombieGame.PerformanceTests
{
    /// <summary>Shared four-neighbour BFS field. Cell-centre routes prevent corner cutting.</summary>
    public sealed class HordeNavigation
    {
        public const int SIZE = 256;
        public readonly bool[] blocked = new bool[SIZE * SIZE];
        public readonly int[] distance = new int[SIZE * SIZE];
        public readonly int[] next_cell = new int[SIZE * SIZE];
        private readonly int[] queue = new int[SIZE * SIZE];
        public int target_cell { get; private set; }

        public HordeNavigation(bool has_obstacles)
        {
            if (has_obstacles)
            {
                add_wall(80, 82, 0, 205);
                add_wall(145, 147, 50, 256);
                add_wall(205, 207, 0, 120);
                add_wall(205, 207, 132, 256);
            }
            build_field(240 + 128 * SIZE);
        }

        private void add_wall(int min_x, int max_x, int min_z, int max_z)
        {
            for (int z = min_z; z < max_z; z++)
                for (int x = min_x; x < max_x; x++) blocked[x + z * SIZE] = true;
        }

        /// <summary>Returns false for blocked/out-of-map goals; preserves the previous field.</summary>
        public bool build_field(int goal)
        {
            if (goal < 0 || goal >= blocked.Length || blocked[goal]) return false;
            target_cell = goal;
            Array.Fill(distance, -1);
            Array.Fill(next_cell, -1);
            int head = 0;
            int tail = 1;
            queue[0] = goal;
            distance[goal] = 0;
            next_cell[goal] = goal;
            while (head < tail)
            {
                int cell = queue[head++];
                int x = cell % SIZE;
                int z = cell / SIZE;
                if (x > 0) visit_cell(cell - 1, cell, ref tail);
                if (x < SIZE - 1) visit_cell(cell + 1, cell, ref tail);
                if (z > 0) visit_cell(cell - SIZE, cell, ref tail);
                if (z < SIZE - 1) visit_cell(cell + SIZE, cell, ref tail);
            }
            return true;
        }

        private void visit_cell(int candidate, int parent, ref int tail)
        {
            if (blocked[candidate] || distance[candidate] >= 0) return;
            distance[candidate] = distance[parent] + 1;
            next_cell[candidate] = parent;
            queue[tail++] = candidate;
        }

        public static Vector3 cell_position(int cell)
        {
            return new Vector3(cell % SIZE - 127.5f, 0.6f, cell / SIZE - 127.5f);
        }
    }

    /// <summary>Allocation-free fixed-tick movement; units deliberately do not collide with each other.</summary>
    public sealed class HordeMovement
    {
        public readonly Vector3[] positions;
        public readonly int[] cells;
        private readonly int[] destinations;
        public readonly bool[] arrived;
        public int arrived_count { get; private set; }
        public int invalid_count { get; private set; }
        public readonly HordeNavigation navigation;
        public static float SPEED => ZombieGame.Balance.UnitBalance.runner.move_speed;

        public HordeMovement(int count, HordeNavigation field)
        {
            navigation = field;
            positions = new Vector3[count];
            cells = new int[count];
            destinations = new int[count];
            arrived = new bool[count];
            for (int i = 0; i < count; i++)
            {
                // Repeatable 64 by 240 spawn strip; no random or off-map starts.
                int cell = 4 + i % 64 + (8 + i / 64 % 240) * HordeNavigation.SIZE;
                cells[i] = destinations[i] = cell;
                positions[i] = HordeNavigation.cell_position(cell);
            }
        }

        public void retarget()
        {
            Array.Clear(arrived, 0, arrived.Length);
            arrived_count = 0;
            // Finish the current safe segment before following the rebuilt field.
        }

        public void step(float delta_seconds)
        {
            for (int i = 0; i < positions.Length; i++)
            {
                if (arrived[i]) continue;
                float remaining = SPEED * delta_seconds;
                while (remaining > 0f)
                {
                    Vector3 waypoint = HordeNavigation.cell_position(destinations[i]);
                    float length = Vector3.Distance(positions[i], waypoint);
                    if (length > remaining)
                    {
                        positions[i] = Vector3.MoveTowards(positions[i], waypoint, remaining);
                        break;
                    }
                    positions[i] = waypoint;
                    remaining -= length;
                    cells[i] = destinations[i];
                    if (cells[i] == navigation.target_cell)
                    {
                        arrived[i] = true;
                        arrived_count++;
                        break;
                    }
                    int next = navigation.next_cell[cells[i]];
                    if (next < 0 || navigation.blocked[next])
                    {
                        invalid_count++;
                        break;
                    }
                    destinations[i] = next;
                }
            }
        }
    }
}
