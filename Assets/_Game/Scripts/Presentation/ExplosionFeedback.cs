using System;
using UnityEngine;
using UnityEngine.Rendering;
using ZombieGame.Balance;
using ZombieGame.Combat;
using ZombieGame.Vision;

namespace ZombieGame.Presentation
{
    /// <summary>Bounded presentation of existing silent explosion events; never emits gameplay damage or noise.</summary>
    public sealed class ExplosionFeedback : IDisposable
    {
        private readonly Mesh cube;
        private readonly Material material;
        private readonly Matrix4x4[] matrices = new Matrix4x4[256 * 24];
        public int visible_events { get; private set; }

        public ExplosionFeedback(Shader shader)
        {
            var primitive = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube = primitive.GetComponent<MeshFilter>().sharedMesh;
            UnityEngine.Object.Destroy(primitive);
            material = new Material(shader) { color = new Color(.6f, .2f, .4f), enableInstancing = true };
        }

        public void draw(BattleSimulation battle, CombatFog fog, bool reveal)
            => draw_events(battle.flashes, fog, reveal);

        public void draw_events(BattleSimulation.Flash[] flashes, CombatFog fog, bool reveal)
        {
            if (flashes.Length > 256) throw new ArgumentException("Explosion presentation exceeds the production event capacity");
            int count = 0; visible_events = 0;
            foreach (var flash in flashes)
            {
                if (flash.expires <= Time.time || !reveal && (fog == null || !fog.is_visible(flash.origin))) continue;
                visible_events++;
                float radius = UnitBalance.exploder.explosion_radius * (1 - (flash.expires - Time.time) / .85f);
                for (int i = 0; i < 24; i++)
                    matrices[count++] = Matrix4x4.TRS(flash.origin + new Vector3(Mathf.Cos(i * Mathf.PI / 12) * radius, .15f,
                        Mathf.Sin(i * Mathf.PI / 12) * radius), Quaternion.identity, new Vector3(.18f, .12f, .18f));
            }
            var parameters = new RenderParams(material) { worldBounds = new Bounds(Vector3.zero, new Vector3(260, 30, 260)), shadowCastingMode = ShadowCastingMode.Off };
            for (int start = 0; start < count; start += 1023)
                Graphics.RenderMeshInstanced(parameters, cube, 0, matrices, Math.Min(1023, count - start), start);
        }

        public void Dispose() => UnityEngine.Object.Destroy(material);
    }
}
