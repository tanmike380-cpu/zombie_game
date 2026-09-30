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
        private readonly Mesh droplet;
        private readonly Material material;
        private readonly Matrix4x4[] matrices = new Matrix4x4[256 * 24];
        public int visible_events { get; private set; }
        public int live_splashes { get; private set; }
        public int emitted_splashes { get; private set; }
        private struct Splash { public Vector3 origin; public float start, end; }
        private readonly Splash[] splashes = new Splash[256];
        private readonly float[] seen = new float[256];
        private int next_splash;

        public ExplosionFeedback(Shader shader)
        {
            droplet = create_slime_fragment();
            material = new Material(shader) { color = new Color(.29f, .022f, .43f), enableInstancing = true };
            if (material.HasProperty("_Glossiness")) material.SetFloat("_Glossiness", .42f);
        }

        public void draw(BattleSimulation battle, CombatFog fog, bool reveal)
            => draw_events(battle.flashes, fog, reveal);

        public void draw_events(BattleSimulation.Flash[] flashes, CombatFog fog, bool reveal)
        {
            if (flashes.Length > 256) throw new ArgumentException("Explosion presentation exceeds the production event capacity");
            int count = 0; visible_events = 0; live_splashes = 0;
            for (int index = 0; index < flashes.Length; index++)
            {
                var flash = flashes[index];
                if (flash.expires <= Time.time || !reveal && (fog == null || !fog.is_visible(flash.origin))) continue;
                visible_events++;
                if (seen[index] == flash.expires) continue;
                seen[index] = flash.expires; emitted_splashes++;
                splashes[next_splash++ % splashes.Length] = new Splash { origin = flash.origin, start = flash.expires - .85f, end = flash.expires + 1.65f };
            }
            for (int index = 0; index < splashes.Length; index++)
            {
                var splash = splashes[index];
                if (splash.end <= Time.time || !reveal && (fog == null || !fog.is_visible(splash.origin))) continue;
                live_splashes++;
                float age = Mathf.Max(0, Time.time - splash.start);
                float fade = Mathf.Clamp01((splash.end - Time.time) / .7f);
                for (int piece = 0; piece < 24; piece++)
                {
                    float angle = piece * 2.39996f + index * .37f;
                    float spread = UnitBalance.exploder.explosion_radius * (.35f + piece % 7 * .11f);
                    float flight = Mathf.Min(age, .65f + piece % 3 * .09f);
                    float vertical_speed = 1.1f + piece % 5 * .38f;
                    float height = .95f + vertical_speed * flight - 7f * flight * flight;
                    bool landed = height <= .035f || age >= .83f;
                    Vector3 velocity = new Vector3(Mathf.Cos(angle)*spread,vertical_speed-14*flight,Mathf.Sin(angle)*spread);
                    Vector3 point = splash.origin + new Vector3(Mathf.Cos(angle)*spread*flight,landed?.035f:height,Mathf.Sin(angle)*spread*flight);
                    float size = (.065f + piece % 4 * .022f) * fade;
                    // Torn sheets and tapered streaks, with smaller secondary drops; never uniformly round balloons.
                    bool sheet = piece % 4 == 0;
                    Vector3 scale = landed ? new Vector3(size*4,.018f*fade,size*3)
                        : new Vector3(size*(sheet?3:1),size*(sheet?3.8f:2.6f),size*(sheet?.28f:.65f));
                    Quaternion rotation = landed ? Quaternion.Euler(0,angle*Mathf.Rad2Deg,0)
                        : Quaternion.FromToRotation(Vector3.up,velocity.normalized)*Quaternion.Euler(0,piece*73,0);
                    matrices[count++] = Matrix4x4.TRS(point, rotation, scale);
                }
            }
            var parameters = new RenderParams(material) { worldBounds = new Bounds(Vector3.zero, new Vector3(260, 30, 260)), shadowCastingMode = ShadowCastingMode.Off };
            for (int start = 0; start < count; start += 1023)
                Graphics.RenderMeshInstanced(parameters, droplet, 0, matrices, Math.Min(1023, count - start), start);
        }

        private static void release_object(UnityEngine.Object item)
        {
            if (Application.isPlaying) UnityEngine.Object.Destroy(item);
            else UnityEngine.Object.DestroyImmediate(item);
        }

        private static Mesh create_slime_fragment()
        {
            const int SIDES=14, RINGS=10;
            var vertices=new Vector3[SIDES*RINGS];
            var triangles=new int[SIDES*(RINGS-1)*6];int cursor=0;
            for(int ring=0;ring<RINGS;ring++)
            {
                float t=ring/(float)(RINGS-1);
                float radius=Mathf.Pow(Mathf.Max(0,Mathf.Sin(t*Mathf.PI)),.65f)*(1.05f-.7f*t);
                for(int side=0;side<SIDES;side++)
                {
                    float angle=side*Mathf.PI*2/SIDES;
                    float lobe=1+.12f*Mathf.Sin(angle*3+ring*.7f);
                    vertices[ring*SIDES+side]=new Vector3(Mathf.Cos(angle)*radius*lobe,t*2-1,Mathf.Sin(angle)*radius*lobe);
                    if(ring==RINGS-1)continue;
                    int a=ring*SIDES+side,b=ring*SIDES+(side+1)%SIDES,c=a+SIDES,d=b+SIDES;
                    triangles[cursor++]=a;triangles[cursor++]=c;triangles[cursor++]=b;
                    triangles[cursor++]=b;triangles[cursor++]=c;triangles[cursor++]=d;
                }
            }
            var result=new Mesh {name="Torn tapered slime fragment",vertices=vertices,triangles=triangles};
            result.RecalculateNormals();result.RecalculateBounds();return result;
        }

        public void Dispose() { release_object(material); release_object(droplet); }
    }
}
