using System;
using UnityEngine;
using UnityEngine.Rendering;
using ZombieGame.Combat;
using ZombieGame.Presentation;
using ZombieGame.Vision;
using ZombieGame.Balance;

namespace ZombieGame.World
{
    public sealed class FrontierBattleView : IDisposable
    {
        private readonly CharacterCrowdRenderer characters;
        private readonly MusketEffects effects;
        private readonly Mesh cube;
        private readonly Material[] materials=new Material[5];
        private readonly Matrix4x4[][] matrices=new Matrix4x4[5][];
        private readonly int[] counts=new int[5];
        private readonly float[] seen_shots,death_started;

        public FrontierBattleView(int capacity,Transform parent,Shader shader)
        {
            characters=new CharacterCrowdRenderer(capacity,VisualStyles.current.id);effects=new MusketEffects(parent);
            seen_shots=new float[capacity];death_started=new float[capacity];
            for(int i=0;i<capacity;i++) seen_shots[i]=float.NegativeInfinity;
            var primitive=GameObject.CreatePrimitive(PrimitiveType.Cube);cube=primitive.GetComponent<MeshFilter>().sharedMesh;UnityEngine.Object.Destroy(primitive);
            Color[] colors={new Color(.47f,.65f,.24f),new Color(.09f,.08f,.045f),new Color(1,.8f,.2f),new Color(.6f,.2f,.4f),new Color(.12f,.52f,1f)};
            for(int i=0;i<materials.Length;i++) { materials[i]=new Material(shader){color=colors[i],enableInstancing=true};matrices[i]=new Matrix4x4[capacity+4096]; }
        }
        public void set_style(){characters.set_style(VisualStyles.current.id);}
        public void draw(BattleSimulation battle,CombatFog fog,bool reveal)
        {
            characters.begin_frame();Array.Clear(counts,0,counts.Length);
            for(int i=0;i<battle.total_count;i++)
            {
                if(battle.is_reserve(i))continue;
                bool human=i<battle.soldier_count;
                float model_scale=human?1:battle.stats_for(i).model_scale;
                if(!human&&!reveal&&!fog.is_visible(battle.positions[i])) continue;
                var agent=battle.crowd.agents[i];
                Vector3 facing=human?battle.soldier_facing[i]:agent.enabled?agent.velocity:Vector3.zero;
                Quaternion rotation=facing.sqrMagnitude>.001f?Quaternion.LookRotation(facing):battle.crowd.transforms[i].rotation;
                if(battle.health[i]<=0)
                {
                    if(death_started[i]==0)death_started[i]=Time.time;
                    if(Time.time-death_started[i]<2) characters.add(human,CharacterPose.Death,Time.time-death_started[i],battle.positions[i],rotation,battle.exploder[i],model_scale);
                    continue;
                }
                float age=Time.time-battle.attack_started_at[i];
                bool moving=agent.enabled&&agent.velocity.sqrMagnitude>.04f;
                var pose=moving?CharacterPose.Run:age<.4f?CharacterPose.Attack:CharacterPose.Idle;
                if(human&&battle.uses_melee(i))pose=moving?CharacterPose.MeleeRun:age<.55f&&battle.last_attack_melee[i]?CharacterPose.MeleeAttack:CharacterPose.MeleeIdle;
                characters.add(human,pose,pose==CharacterPose.Attack||pose==CharacterPose.MeleeAttack?age:Time.time+i*.137f,battle.positions[i],rotation,battle.exploder[i],model_scale);
                if(human&&!battle.last_attack_melee[i]&&battle.attack_started_at[i]>seen_shots[i])
                { seen_shots[i]=battle.attack_started_at[i];effects.fire(characters.human_muzzle(battle.positions[i],rotation),rotation*Vector3.forward); }
                if(human&&battle.selected[i])
                {
                    Vector3 p=battle.positions[i]+Vector3.up*.045f;
                    add(0,p+Vector3.left*.36f,new Vector3(.035f,.025f,.72f));
                    add(0,p+Vector3.right*.36f,new Vector3(.035f,.025f,.72f));
                    add(0,p+Vector3.forward*.36f,new Vector3(.72f,.025f,.035f));
                    add(0,p+Vector3.back*.36f,new Vector3(.72f,.025f,.035f));
                }
                if((human&&(battle.selected[i]||battle.ammunition[i]<UnitBalance.human.ammunition_capacity))||battle.health[i]<battle.stats_for(i).health)
                {
                    float health=Mathf.Clamp01(battle.health[i]/battle.stats_for(i).health);
                    Vector3 point=battle.positions[i]+Vector3.up*1.9f*model_scale;
                    draw_status_bar(point,health,0);
                    if(human) draw_status_bar(point-Camera.main.transform.up*.15f,
                        battle.ammunition[i]/(float)UnitBalance.human.ammunition_capacity,4);
                }
            }
            foreach(var shot in battle.projectiles) if(shot.active&&(reveal||fog.is_visible(shot.position))) add(2,shot.position,Vector3.one*.13f);
            foreach(var shot in battle.enemy_projectiles)if(shot.active&&(reveal||fog.is_visible(shot.position)))add(0,shot.position,Vector3.one*.28f);
            foreach(var impact in battle.enemy_impacts)if(impact.expires>Time.time&&(reveal||fog.is_visible(impact.origin)))
                for(int i=0;i<32;i++)add(impact.acid?0:2,impact.origin+new Vector3(Mathf.Cos(i*Mathf.PI/16)*impact.radius,.12f,Mathf.Sin(i*Mathf.PI/16)*impact.radius),new Vector3(.18f,.08f,.18f));
            foreach(var flash in battle.flashes) if(flash.expires>Time.time&&(reveal||fog.is_visible(flash.origin)))
            {
                float radius=UnitBalance.exploder.explosion_radius*(1-(flash.expires-Time.time)/.85f);
                for(int i=0;i<24;i++) add(3,flash.origin+new Vector3(Mathf.Cos(i*Mathf.PI/12)*radius,.15f,Mathf.Sin(i*Mathf.PI/12)*radius),new Vector3(.18f,.12f,.18f));
            }
            characters.draw();
            for(int i=0;i<materials.Length;i++)
            {
                var parameters=new RenderParams(materials[i]){worldBounds=new Bounds(Vector3.zero,new Vector3(260,30,260)),shadowCastingMode=ShadowCastingMode.Off};
                for(int start=0;start<counts[i];start+=1023) Graphics.RenderMeshInstanced(parameters,cube,0,matrices[i],Math.Min(1023,counts[i]-start),start);
            }
        }
        private void add(int group,Vector3 point,Vector3 size)
        { if(counts[group]<matrices[group].Length)matrices[group][counts[group]++]=Matrix4x4.TRS(point,Quaternion.identity,size); }
        private void draw_status_bar(Vector3 point,float fraction,int color_group)
        {
            var camera_transform=Camera.main.transform;
            fraction=Mathf.Clamp01(fraction);
            if(counts[1]<matrices[1].Length)matrices[1][counts[1]++]=Matrix4x4.TRS(point,camera_transform.rotation,new Vector3(.74f,.1f,.025f));
            if(fraction<=0||counts[color_group]>=matrices[color_group].Length)return;
            Vector3 fill_point=point-camera_transform.right*(.34f*(1-fraction))-camera_transform.forward*.025f;
            matrices[color_group][counts[color_group]++]=Matrix4x4.TRS(fill_point,camera_transform.rotation,new Vector3(.68f*fraction,.06f,.025f));
        }
        public void Dispose() { effects.Dispose();foreach(var material in materials)UnityEngine.Object.Destroy(material); }
    }
}
