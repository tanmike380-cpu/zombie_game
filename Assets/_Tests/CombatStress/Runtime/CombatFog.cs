using System;
using UnityEngine;

namespace ZombieGame.CombatStressTests
{
    /// <summary>One-tile fog grid: human radius 10, persistent exploration, no height occlusion yet.</summary>
    public sealed class CombatFog : IDisposable
    {
        public const int SIZE = 256;
        public const float HUMAN_SIGHT = 10;
        private readonly bool[] visible = new bool[SIZE * SIZE], explored = new bool[SIZE * SIZE];
        private readonly Color32[] pixels = new Color32[SIZE * SIZE];
        private readonly Texture2D texture;
        private readonly Material material;
        private readonly Mesh quad;

        public CombatFog(Material template)
        {
            if (template == null) throw new InvalidOperationException("Missing serialized fog material");
            texture = new Texture2D(SIZE, SIZE, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            material = new Material(template); material.mainTexture = texture;
            var primitive = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad = primitive.GetComponent<MeshFilter>().sharedMesh; UnityEngine.Object.Destroy(primitive);
        }

        public void update_visibility(CombatStressSimulation simulation)
        {
            Array.Clear(visible, 0, visible.Length);
            for (int i = 0; i < CombatStressSimulation.SOLDIERS; i++)
            {
                if (simulation.health[i] <= 0) continue;
                Vector3 center = simulation.positions[i];
                int min_x = Mathf.Max(0, Mathf.FloorToInt(center.x + 128 - HUMAN_SIGHT));
                int max_x = Mathf.Min(255, Mathf.FloorToInt(center.x + 128 + HUMAN_SIGHT));
                int min_z = Mathf.Max(0, Mathf.FloorToInt(center.z + 128 - HUMAN_SIGHT));
                int max_z = Mathf.Min(255, Mathf.FloorToInt(center.z + 128 + HUMAN_SIGHT));
                for (int z = min_z; z <= max_z; z++)
                    for (int x = min_x; x <= max_x; x++)
                    {
                        float dx = x - 127.5f - center.x, dz = z - 127.5f - center.z;
                        if (dx * dx + dz * dz <= HUMAN_SIGHT * HUMAN_SIGHT)
                            visible[x + z * SIZE] = explored[x + z * SIZE] = true;
                    }
            }
            for (int i = 0; i < pixels.Length; i++) pixels[i] = new Color32(0, 0, 0, visible[i] ? (byte)0 : explored[i] ? (byte)175 : (byte)255);
            texture.SetPixels32(pixels); texture.Apply(false, false);
        }

        public bool is_visible(Vector3 point)
        {
            int x = Mathf.FloorToInt(point.x + 128), z = Mathf.FloorToInt(point.z + 128);
            return x >= 0 && x < SIZE && z >= 0 && z < SIZE && visible[x + z * SIZE];
        }

        public void draw()
        {
            Graphics.DrawMesh(quad, Matrix4x4.TRS(new Vector3(0, 8, 0), Quaternion.Euler(90, 0, 0), new Vector3(256,256,1)), material, 0);
        }

        public void Dispose() { UnityEngine.Object.Destroy(texture); UnityEngine.Object.Destroy(material); }
    }
}
