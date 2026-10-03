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
        private readonly LineRenderer[] cores=new LineRenderer[32];
        private float next_emission;
        public int active_jets {get;private set;}
        public int particle_count=>particles.particleCount;
        public FlameEffects(Transform parent)
        {
            root=new GameObject("Shared flame jets");root.transform.SetParent(parent,false);
            particles=root.AddComponent<ParticleSystem>();particles.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            var main=particles.main;main.playOnAwake=false;main.maxParticles=4096;main.simulationSpace=ParticleSystemSimulationSpace.World;main.startSpeed=0;
            var emission=particles.emission;emission.enabled=false;var shape=particles.shape;shape.enabled=false;
            var color=particles.colorOverLifetime;color.enabled=true;
            var gradient=new Gradient();gradient.SetKeys(new[]{new GradientColorKey(new Color(1,.8f,.22f),0),new GradientColorKey(new Color(1,.24f,.015f),.6f),new GradientColorKey(new Color(.6f,.035f,.004f),1)},new[]{new GradientAlphaKey(.95f,0),new GradientAlphaKey(.85f,.65f),new GradientAlphaKey(0,1)});color.color=gradient;
            var size=particles.sizeOverLifetime;size.enabled=true;size.size=new ParticleSystem.MinMaxCurve(1,AnimationCurve.Linear(0,.3f,1,1));
            var shader=Resources.Load<Shader>("Effects/GreekFlame");
            if(shader==null)throw new System.InvalidOperationException("Missing Greek flame presentation shader");
            material=new Material(shader);
            root.GetComponent<ParticleSystemRenderer>().sharedMaterial=material;particles.Play();
            for(int i=0;i<cores.Length;i++)
            {
                var jet=new GameObject("Flame core "+i);jet.transform.SetParent(root.transform,false);
                var line=jet.AddComponent<LineRenderer>();line.sharedMaterial=material;line.useWorldSpace=true;line.positionCount=14;
                line.startColor=new Color(1,.7f,.15f,.9f);line.endColor=new Color(1,.15f,.005f,0);
                line.widthCurve=new AnimationCurve(new Keyframe(0,.18f),new Keyframe(.4f,.5f),new Keyframe(.8f,.75f),new Keyframe(1,.05f));
                line.enabled=false;cores[i]=line;
            }
        }
        public static float jet_length(Vector3 origin,Vector3 muzzle,Vector3 direction,float range)
            =>Mathf.Max(0,range-Mathf.Max(0,Vector3.Dot(muzzle-origin,direction.normalized)));
        public void draw(BattleSimulation battle,GreekFireView equipment)
        {
            bool emit=Time.time>=next_emission&&Time.deltaTime>0;
            if(emit)next_emission=Time.time+.025f;
            active_jets=0;
            foreach(var burst in battle.flame_bursts)
            {
                if(burst.expires<=Time.time||active_jets==cores.Length)continue;
                Vector3 muzzle=burst.source>=0?equipment.muzzle(burst.source,burst.origin):burst.origin+Vector3.up*1.6f;
                float length=jet_length(burst.origin,muzzle,burst.direction,burst.stats.attack_range);
                var core=cores[active_jets++];core.enabled=true;
                Vector3 lateral=Vector3.Cross(Vector3.up,burst.direction);
                for(int point=0;point<core.positionCount;point++)
                {
                    float fraction=point/(float)(core.positionCount-1);
                    core.SetPosition(point,muzzle+burst.direction*(length*fraction)+Vector3.up*(.18f*fraction*fraction)
                        +lateral*(Mathf.Sin(fraction*19-Time.time*20)*.1f*fraction));
                }
                if(!emit)continue;
                for(int i=0;i<15;i++)
                {
                    float angle=(i%5-2)*burst.stats.cone_angle/6;
                    Vector3 direction=Quaternion.Euler(0,angle,0)*burst.direction;
                    particles.Emit(new ParticleSystem.EmitParams{position=muzzle,velocity=direction*burst.stats.projectile_speed+Vector3.up*.25f,
                        startLifetime=length/burst.stats.projectile_speed,startSize=1.35f,startColor=Color.white,rotation=i*47+Time.time*100},1);
                }
            }
            for(int i=active_jets;i<cores.Length;i++)cores[i].enabled=false;
        }
        public void Dispose(){UnityEngine.Object.Destroy(root);UnityEngine.Object.Destroy(material);}
    }
}
