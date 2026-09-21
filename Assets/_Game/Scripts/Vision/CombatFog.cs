using System;
using ZombieGame.Combat;
using ZombieGame.Balance;
using UnityEngine;

namespace ZombieGame.Vision
{
    /// <summary>One-tile fog grid: human radius 10, persistent exploration, no height occlusion yet.</summary>
    public sealed class CombatFog : IDisposable
    {
        public const int SIZE = 256;
        public static float HUMAN_SIGHT => UnitBalance.config.human_sight;
        private readonly bool[] visible = new bool[SIZE * SIZE], explored = new bool[SIZE * SIZE];
        private readonly Color32[] pixels = new Color32[SIZE * SIZE];
        private readonly Texture2D texture;
        private readonly Material material;
        private readonly Mesh quad;
        private readonly float plane_height;
        public Vector3? headquarters_vision;

        public CombatFog(Material template, float plane_height = 8, bool smooth_edges = false)
        {
            this.plane_height=plane_height;
            if (template == null) throw new InvalidOperationException("Missing serialized fog material");
            texture = new Texture2D(SIZE, SIZE, TextureFormat.RGBA32, false) { filterMode = smooth_edges?FilterMode.Bilinear:FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            material = new Material(template); material.mainTexture = texture;
            var primitive = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad = primitive.GetComponent<MeshFilter>().sharedMesh; UnityEngine.Object.Destroy(primitive);
        }

        public void update_visibility(BattleSimulation simulation)
        {
            Array.Clear(visible, 0, visible.Length);
            for (int i = 0; i < simulation.soldier_count; i++)
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
            if(headquarters_vision.HasValue)
            {
                Vector3 center=headquarters_vision.Value;
                for(int z=Mathf.Max(0,Mathf.FloorToInt(center.z+128-HUMAN_SIGHT));z<=Mathf.Min(255,Mathf.CeilToInt(center.z+128+HUMAN_SIGHT));z++)
                    for(int x=Mathf.Max(0,Mathf.FloorToInt(center.x+128-HUMAN_SIGHT));x<=Mathf.Min(255,Mathf.CeilToInt(center.x+128+HUMAN_SIGHT));x++)
                        if((new Vector3(x-127.5f,0,z-127.5f)-center).sqrMagnitude<=HUMAN_SIGHT*HUMAN_SIGHT)
                            visible[x+z*SIZE]=explored[x+z*SIZE]=true;
            }
            for (int i = 0; i < pixels.Length; i++) pixels[i] = new Color32(0, 0, 0, visible[i] ? (byte)0 : explored[i] ? (byte)175 : (byte)255);
            texture.SetPixels32(pixels); texture.Apply(false, false);
        }

        public bool is_visible(Vector3 point)
        {
            int x = Mathf.FloorToInt(point.x + 128), z = Mathf.FloorToInt(point.z + 128);
            return x >= 0 && x < SIZE && z >= 0 && z < SIZE && visible[x + z * SIZE];
        }

        public void explore_area(Rect area)
        {
            for(int z=0;z<SIZE;z++) for(int x=0;x<SIZE;x++)
                if(area.Contains(new Vector2(x-127.5f,z-127.5f))) explored[x+z*SIZE]=true;
        }

        public Color32 minimap_color(int index, bool reveal)
        {
            if (reveal || visible[index]) return new Color32(41,64,31,255);
            return explored[index] ? new Color32(17,26,13,255) : new Color32(0,0,0,255);
        }

        public bool is_explored(Vector3 point)
        {
            int x = Mathf.FloorToInt(point.x + 128), z = Mathf.FloorToInt(point.z + 128);
            return x >= 0 && x < SIZE && z >= 0 && z < SIZE && explored[x + z * SIZE];
        }

        public void draw()
        {
            Graphics.DrawMesh(quad, Matrix4x4.TRS(new Vector3(0, plane_height, 0), Quaternion.Euler(90, 0, 0), new Vector3(256,256,1)), material, 0);
        }

        public void Dispose() { UnityEngine.Object.Destroy(texture); UnityEngine.Object.Destroy(material); }
    }
}
