using UnityEngine;

namespace ZombieGame.CombatStressTests
{
    /// <summary>Allocation-free spatial broad phase; routing remains native NavMesh.</summary>
    public sealed class CombatSpatialGrid
    {
        public const int SIDE = 32;
        public const float CELL = 8;
        public readonly int[] heads = new int[SIDE * SIDE];
        public readonly int[] next;

        public CombatSpatialGrid(int count) { next = new int[count]; clear(); }
        public static int cell(float coordinate) => Mathf.Clamp(Mathf.FloorToInt((coordinate + 128) / CELL), 0, SIDE - 1);
        public void clear() { for (int i = 0; i < heads.Length; i++) heads[i] = -1; }
        public void insert(int index, Vector3 position)
        {
            int key = cell(position.x) + cell(position.z) * SIDE;
            next[index] = heads[key]; heads[key] = index;
        }
    }
}
