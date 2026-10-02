using UnityEngine;
using ZombieGame.Combat;

namespace ZombieGame.Presentation
{
    /// <summary>One bounded particle pool; visual jets follow combat bursts and never apply damage.</summary>
    public sealed class FlameEffects:System.IDisposable
    {
        private readonly GameObject root;
        private readonly ParticleSystem particles;
        private readonly Material material;
        private float next_emission;
        public FlameEffects(Transform parent)
        {
            root=new GameObject("Shared flame jets");root.transform.SetParent(parent,false);
            particles=root.AddComponent<ParticleSystem>();particles.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            var main=particles.main;main.playOnAwake=false;main.maxParticles=1800;main.simulationSpace=ParticleSystemSimulationSpace.World;main.startSpeed=0;
            var emission=particles.emission;emission.enabled=false;var shape=particles.shape;shape.enabled=false;
            var color=particles.colorOverLifetime;color.enabled=true;
            var gradient=new Gradient();gradient.SetKeys(new[]{new GradientColorKey(new Color(1,.9f,.3f),0),new GradientColorKey(new Color(1,.22f,.025f),.55f),new GradientColorKey(new Color(.2f,.1f,.06f),1)},new[]{new GradientAlphaKey(.9f,0),new GradientAlphaKey(.6f,.6f),new GradientAlphaKey(0,1)});color.color=gradient;
            var size=particles.sizeOverLifetime;size.enabled=true;size.size=new ParticleSystem.MinMaxCurve(1,AnimationCurve.Linear(0,.25f,1,1));
            material=new Material(Resources.Load<Material>("CharacterGenerated/MusketSmoke"));
            root.GetComponent<ParticleSystemRenderer>().sharedMaterial=material;particles.Play();
        }
        public void draw(BattleSimulation battle,GreekFireView equipment)
        {
            if(Time.time<next_emission||Time.deltaTime<=0)return;next_emission=Time.time+.035f;
            foreach(var burst in battle.flame_bursts)
            {
                if(burst.expires<=Time.time)continue;
                Vector3 muzzle=burst.source>=0?equipment.muzzle(burst.source,burst.origin):burst.origin+Vector3.up*1.6f;
                for(int i=0;i<5;i++)
                {
                    float angle=(i-2)*burst.stats.cone_angle/5;
                    Vector3 direction=Quaternion.Euler(0,angle,0)*burst.direction;
                    particles.Emit(new ParticleSystem.EmitParams{position=muzzle,velocity=direction*burst.stats.projectile_speed+Vector3.up*.25f,
                        startLifetime=burst.stats.attack_range/burst.stats.projectile_speed,startSize=.75f,startColor=Color.white},1);
                }
            }
        }
        public void Dispose(){UnityEngine.Object.Destroy(root);UnityEngine.Object.Destroy(material);}
    }
}
