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
                if (simulation.health[i] <= 0 || simulation.is_reserve(i)) continue;
                reveal_area(simulation.positions[i]);
            }
            foreach(var building in simulation.buildings)
            {
                if(building.health>0&&!building.infected)reveal_area(building.bounds.center);
            }
            for (int i = 0; i < pixels.Length; i++) pixels[i] = new Color32(0, 0, 0, visible[i] ? (byte)0 : explored[i] ? (byte)175 : (byte)255);
            texture.SetPixels32(pixels); texture.Apply(false, false);
        }

        /// <summary>Reveal a ground-plane circle using the shared human sight, regardless of model height.</summary>
        private void reveal_area(Vector3 center)
        {
            float radius=HUMAN_SIGHT;
            int min_z=Mathf.Max(0,Mathf.CeilToInt(center.z+127.5f-radius));
            int max_z=Mathf.Min(SIZE-1,Mathf.FloorToInt(center.z+127.5f+radius));
            for(int z=min_z;z<=max_z;z++)
            {
                float dz=z-127.5f-center.z;
                float half_width=Mathf.Sqrt(Mathf.Max(0,radius*radius-dz*dz));
                int min_x=Mathf.Max(0,Mathf.CeilToInt(center.x+127.5f-half_width));
                int max_x=Mathf.Min(SIZE-1,Mathf.FloorToInt(center.x+127.5f+half_width));
                for(int x=min_x;x<=max_x;x++)visible[x+z*SIZE]=explored[x+z*SIZE]=true;
            }
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
