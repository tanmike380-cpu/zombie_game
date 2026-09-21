using System;
using UnityEngine;
using ZombieGame.Combat;
using ZombieGame.Vision;

namespace ZombieGame.Presentation
{
    /// <summary>Bounded presentation audio, independent of gameplay noise propagation.</summary>
    public sealed class BattleAudio : IDisposable
    {
        public const int VOICE_LIMIT=16;
        private readonly GameObject root;
        private readonly AudioSource[] sources=new AudioSource[VOICE_LIMIT];
        private readonly int[] categories=new int[VOICE_LIMIT];
        private readonly AudioClip gun,knife;
        private readonly AudioClip[] growls=new AudioClip[3];
        private readonly float[] seen_attacks,next_growl;
        private readonly AudioListener owned_listener;
        private readonly System.Random random=new System.Random(821);
        private bool paused;
        private float next_roar;
        public int played_shots {get;private set;}
        public int played_stabs {get;private set;}
        public int played_growls {get;private set;}
        public int active_voices {get {int count=0;foreach(var source in sources)if(source.isPlaying)count++;return count;}}

        public BattleAudio(int capacity,Transform parent)
        {
            root=new GameObject("Battle Audio Pool");root.transform.SetParent(parent,false);
            gun=load_clip("Musket");knife=load_clip("Knife");
            for(int i=0;i<3;i++)growls[i]=load_clip("Zombie"+(i+1));
            seen_attacks=new float[capacity];next_growl=new float[capacity];
            for(int i=0;i<capacity;i++)seen_attacks[i]=float.NegativeInfinity;
            if(UnityEngine.Object.FindFirstObjectByType<AudioListener>()==null)
                owned_listener=root.AddComponent<AudioListener>();
            for(int i=0;i<VOICE_LIMIT;i++)
            {
                var holder=new GameObject("Voice "+i);holder.transform.SetParent(root.transform,false);
                sources[i]=holder.AddComponent<AudioSource>();sources[i].playOnAwake=false;
                // Screen-space stereo avoids the elevated RTS camera attenuating every sound.
                sources[i].spatialBlend=0;sources[i].dopplerLevel=0;
            }
        }
        private static AudioClip load_clip(string name)
        {
            var clip=Resources.Load<AudioClip>("Audio/"+name);
            if(clip==null||clip.length<=0)throw new InvalidOperationException("Missing battle audio: "+name);
            return clip;
        }
        public void update(BattleSimulation battle,CombatFog fog,bool reveal,Vector3 focus,Camera camera,bool is_paused)
        {
            if(paused!=is_paused)
            {paused=is_paused;foreach(var source in sources){if(paused)source.Pause();else source.UnPause();}}
            if(paused)return;
            for(int i=0;i<battle.total_count;i++)
            {
                if(battle.health[i]<=0||battle.is_reserve(i))continue;
                bool human=i<battle.soldier_count;
                bool attacked=battle.attack_started_at[i]>seen_attacks[i];
                seen_attacks[i]=battle.attack_started_at[i];
                Vector3 delta=battle.positions[i]-focus;
                float distance=delta.magnitude;
                if(distance>=45||(!human&&!reveal&&!fog.is_visible(battle.positions[i])))continue;
                float gain=Mathf.Clamp01(1-distance/45);
                float pan=Mathf.Clamp(Vector3.Dot(delta,camera.transform.right)/20,-1,1);
                if(human&&attacked)
                    play(battle.last_attack_melee[i]?1:0,gain,pan);
                else if(!human&&battle.activated[i]&&Time.time>=next_growl[i]&&Time.time>=next_roar&&
                    (battle.crowd.agents[i].velocity.sqrMagnitude>.04f||attacked))
                {
                    next_growl[i]=Time.time+4+(float)random.NextDouble()*3;
                    if(play(2,gain,pan))next_roar=Time.time+.35f;
                }
            }
        }
        /// <summary>Separate caps reserve voices for growls and stabs during mass volleys.</summary>
        public bool play(int category,float gain,float pan)
        {
            if(category<0||category>2)throw new ArgumentOutOfRangeException(nameof(category));
            int count=0,free=-1;
            for(int i=0;i<sources.Length;i++)
                if(sources[i].isPlaying){if(categories[i]==category)count++;}else if(free<0)free=i;
            if(free<0||count>=(category==0?8:4))return false;
            var source=sources[free];categories[free]=category;
            source.clip=category==0?gun:category==1?knife:growls[random.Next(3)];
            source.volume=Mathf.Clamp01(gain)*(category==0?.045f:category==1?.055f:.08f);
            source.panStereo=Mathf.Clamp(pan,-1,1);
            source.pitch=category==2?.8f+(float)random.NextDouble()*.2f:.95f+(float)random.NextDouble()*.1f;
            source.Play();
            if(category==0)played_shots++;else if(category==1)played_stabs++;else played_growls++;
            return true;
        }
        public void Dispose()
        {
            foreach(var source in sources)source.Stop();
            if(owned_listener!=null)owned_listener.enabled=false;
            UnityEngine.Object.Destroy(root);
        }
    }
}
