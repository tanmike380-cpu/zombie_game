using System;
using UnityEngine;
using ZombieGame.Combat;
namespace ZombieGame.CombatStressTests
{
    /// <summary>Benchmark population/map fixture only; gameplay lives in BattleSimulation.</summary>
    public sealed class CombatStressSimulation : BattleSimulation
    {
        public const int SOLDIERS = 400, ZOMBIES = 10000, TOTAL = SOLDIERS + ZOMBIES;
        private struct Setup { public Vector3[] positions; public Bounds[] walls; public bool[] explosive; }
        private readonly int[] lane_targets = new int[100];
        private int lane_frame = -1;
        public CombatStressSimulation(bool global_assault, bool player_controlled = false)
            : this(create_setup(player_controlled), global_assault, player_controlled) { }
        private CombatStressSimulation(Setup setup, bool global_assault, bool player_controlled)
            : base(setup.positions, SOLDIERS, setup.explosive, setup.walls, global_assault, player_controlled)
        { forced_target = find_assault_target; }
        private static Setup create_setup(bool player_controlled)
        {
            var positions = new Vector3[TOTAL];
            var walls = new[] {
                new Bounds(new Vector3(10, 1.5f, -35), new Vector3(3, 3, 28)),
                new Bounds(new Vector3(27, 1.5f, 20), new Vector3(3, 3, 34)),
                new Bounds(new Vector3(47, 1.5f, -15), new Vector3(4, 3, 32)),
                new Bounds(new Vector3(68, 1.5f, 39), new Vector3(3, 3, 30)),
                new Bounds(new Vector3(85, 1.5f, -42), new Vector3(4, 3, 26))
                ,new Bounds(new Vector3(36, .2f, -53), new Vector3(22, .4f, 16))
                ,new Bounds(new Vector3(64, .2f, 8), new Vector3(24, .4f, 14))
                ,new Bounds(new Vector3(-62, 1.5f, 0), new Vector3(3, 3, 22))
                ,new Bounds(new Vector3(-40, 1.5f, 14), new Vector3(3, 3, 20))
                ,new Bounds(new Vector3(-40, 1.5f, -18), new Vector3(3, 3, 16))
            };
            for (int i = 0; i < SOLDIERS; i++)
                positions[i] = player_controlled ? new Vector3(-90 + i % 20 * .9f, 0, -8.55f + i / 20 * .9f)
                    : new Vector3(-15 - i / 100 * .85f, 0, -79.2f + i % 100 * 1.6f);
            int filled = SOLDIERS;
            for (int slot = 0; filled < TOTAL; slot++)
            {
                Vector3 point = new Vector3(-8.1f + slot % 100 * 1.16f, 0, -66 + slot / 100 * 1.16f);
                if (point.z > 120) throw new InvalidOperationException("Stress spawn capacity exceeded");
                bool blocked = false;
                foreach (var wall in walls)
                {
                    Bounds expanded = wall; expanded.Expand(1.2f); point.y = wall.center.y;
                    if (expanded.Contains(point)) blocked = true;
                }
                point.y = 0;
                if (!blocked) positions[filled++] = point;
            }

            var explosive = new bool[TOTAL];
            for (int i = SOLDIERS; i < TOTAL; i++) explosive[i] = (i - SOLDIERS) % 10 == 0;
            return new Setup { positions = positions, walls = walls, explosive = explosive };
        }
        private int find_assault_target(int index)
        {
            if (lane_frame != Time.frameCount)
            {
                lane_frame = Time.frameCount;
                for (int row = 0; row < 100; row++)
                {
                    int best = -1; float score = float.PositiveInfinity;
                    for (int i = 0; i < SOLDIERS; i++)
                    {
                        float candidate = Mathf.Abs(row - i % 100) * 2 + i / 100 * .1f;
                        if (health[i] > 0 && candidate < score) { score = candidate; best = i; }
                    }
                    lane_targets[row] = best;
                }
            }
            return lane_targets[Mathf.Clamp(Mathf.RoundToInt((positions[index].z + 79.2f) / 1.6f), 0, 99)];
        }
    }
}
