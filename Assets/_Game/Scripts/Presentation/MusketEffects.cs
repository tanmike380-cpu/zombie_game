using UnityEngine;

namespace ZombieGame.Presentation
{
    /// <summary>One pooled smoke/flash emitter per battle. Visual only: no damage, noise or navigation calls.</summary>
    public sealed class MusketEffects : System.IDisposable
    {
        private readonly GameObject root;
        private readonly ParticleSystem smoke, flash;
        private readonly Material smoke_material, flash_material;
        public int shot_count { get; private set; }
        public int live_particles => smoke.particleCount + flash.particleCount;

        public MusketEffects(Transform parent = null)
        {
            root = new GameObject("Pooled Musket Smoke"); root.transform.SetParent(parent, false);
            var template = Resources.Load<Material>("CharacterGenerated/MusketSmoke");
            Shader shader = template != null ? template.shader : Shader.Find("ZombieGame/MusketSmoke");
            if (shader == null) throw new System.InvalidOperationException("Missing musket smoke shader");
            smoke_material = new Material(shader); flash_material = new Material(shader);
            smoke = create_emitter("White-grey smoke", smoke_material, 2500);
            flash = create_emitter("Muzzle flash", flash_material, 500);
        }

        private ParticleSystem create_emitter(string name, Material material, int capacity)
        {
            var holder = new GameObject(name); holder.transform.SetParent(root.transform, false);
            var system = holder.AddComponent<ParticleSystem>(); system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = system.main; main.playOnAwake = false; main.loop = true; main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = capacity; main.startSpeed = 0; main.startLifetime = 1; main.startSize = .25f;
            var emission = system.emission; emission.enabled = false;
            var shape = system.shape; shape.enabled = false;
            var size = system.sizeOverLifetime; size.enabled = true; size.size = new ParticleSystem.MinMaxCurve(1, AnimationCurve.Linear(0,.35f,1,1));
            var color = system.colorOverLifetime; color.enabled = true;
            var gradient = new Gradient(); gradient.SetKeys(new[] { new GradientColorKey(Color.white,0),new GradientColorKey(Color.white,1) },
                new[] { new GradientAlphaKey(.9f,0),new GradientAlphaKey(.5f,.4f),new GradientAlphaKey(0,1) });
            color.color = gradient;
            var renderer = holder.GetComponent<ParticleSystemRenderer>(); renderer.sharedMaterial = material;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            system.Play();
            return system;
        }

        public void fire(Vector3 muzzle, Vector3 direction)
        {
            direction.y = Mathf.Max(.08f,direction.y); direction.Normalize(); shot_count++;
            for (int i = 0; i < 5; i++)
            {
                float angle = (shot_count * 2.39996f + i * 1.25664f);
                var drift = new Vector3(Mathf.Cos(angle),.35f,Mathf.Sin(angle)) * .25f;
                smoke.Emit(new ParticleSystem.EmitParams { position = muzzle + direction * i * .04f,
                    velocity = direction * (.8f + i * .12f) + Vector3.up * .3f + drift,
                    startLifetime = .85f + i * .1f, startSize = .7f + i * .06f,
                    startColor = new Color(.79f,.80f,.77f,.65f) }, 1);
            }
            flash.Emit(new ParticleSystem.EmitParams { position = muzzle, velocity = direction * .4f,
                startLifetime = .065f, startSize = .32f, startColor = new Color(1,.75f,.25f,1) },1);
        }

        public void Dispose()
        {
            if (root != null) UnityEngine.Object.Destroy(root);
            UnityEngine.Object.Destroy(smoke_material); UnityEngine.Object.Destroy(flash_material);
        }

        public void clear() { smoke.Clear(); flash.Clear(); shot_count = 0; }
    }
}
