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
        private readonly ExplosionFeedback explosions;
        private readonly UnitFeedbackOverlay feedback=new UnitFeedbackOverlay();
        private readonly Mesh cube;
        private readonly Material[] materials=new Material[5];
        private readonly Matrix4x4[][] matrices=new Matrix4x4[5][];
        private readonly int[] counts=new int[5];
        private readonly float[] seen_shots,death_started;
        private readonly float[] travelled;
        private readonly Quaternion[] stable_rotations;
        private readonly GreekFireView equipment;
        private readonly FlameEffects flames;

        public FrontierBattleView(int capacity,Transform parent,Shader shader,ImportedRoster roster=null,bool strict_imports=false)
        {
            characters=new CharacterCrowdRenderer(capacity,VisualStyles.current.id,roster:roster,strict_imports:strict_imports);effects=new MusketEffects(parent);
            explosions=new ExplosionFeedback(shader);
            equipment=new GreekFireView(parent);flames=new FlameEffects(parent);stable_rotations=new Quaternion[capacity];for(int i=0;i<capacity;i++)stable_rotations[i]=Quaternion.identity;
            seen_shots=new float[capacity];death_started=new float[capacity];
            travelled=new float[capacity];
            for(int i=0;i<capacity;i++) seen_shots[i]=float.NegativeInfinity;
            var primitive=GameObject.CreatePrimitive(PrimitiveType.Cube);cube=primitive.GetComponent<MeshFilter>().sharedMesh;UnityEngine.Object.Destroy(primitive);
            Color[] colors={new Color(.47f,.65f,.24f),new Color(.09f,.08f,.045f),new Color(1,.8f,.2f),new Color(.6f,.2f,.4f),new Color(.12f,.52f,1f)};
            for(int i=0;i<materials.Length;i++) { materials[i]=new Material(shader){color=colors[i],enableInstancing=true};matrices[i]=new Matrix4x4[capacity+4096]; }
        }
        public void set_style(){characters.set_style(VisualStyles.current.id);}
        public Vector3 projectile_origin(BattleSimulation battle,int index)
        {
            var forward=battle.soldier_facing[index];forward.y=0;
            return characters.unit_muzzle(battle.stats_for(index).id,battle.positions[index],forward.sqrMagnitude>.0001f?Quaternion.LookRotation(forward):Quaternion.identity);
        }
        public void draw(BattleSimulation battle,CombatFog fog,bool reveal)
        {
            characters.begin_frame(Camera.main);Array.Clear(counts,0,counts.Length);
            for(int i=0;i<battle.total_count;i++)
            {
                if(battle.is_reserve(i))continue;
                travelled[i]+=battle.observed_velocity[i].magnitude*Time.deltaTime;
                if(battle.stats_for(i).id=="greek_fire")continue;
                bool human=i<battle.soldier_count;
                float model_scale=human?1:battle.stats_for(i).model_scale;
                if(!human&&!reveal&&!fog.is_visible(battle.positions[i])) continue;
                var agent=battle.crowd.agents[i];
                bool attacking=!human&&Time.time-battle.attack_started_at[i]<ZombieAnimation.attack_duration(battle.stats_for(i),battle.exploder[i]);
                Vector3 facing=human?battle.soldier_facing[i]:attacking?battle.attack_facing[i]:battle.observed_velocity[i];facing.y=0;
                if(facing.sqrMagnitude>(attacking||human?.001f:.09f))
                    stable_rotations[i]=human?Quaternion.LookRotation(facing):Quaternion.RotateTowards(stable_rotations[i],Quaternion.LookRotation(facing),240*Time.deltaTime);
                Quaternion rotation=stable_rotations[i];
                if(battle.health[i]<=0)
                {
                    if(death_started[i]==0)death_started[i]=Time.time;
                    if(Time.time-death_started[i]<2) characters.add(human,CharacterPose.Death,Time.time-death_started[i],battle.positions[i],rotation,battle.exploder[i],model_scale,battle.stats_for(i).id);
                    continue;
                }
                float age=Time.time-battle.attack_started_at[i];
                bool moving=agent.enabled&&battle.observed_velocity[i].sqrMagnitude>.04f;
                float ranged_window=human?Mathf.Max(.4f,battle.stats_for(i).attack_interval*.95f):.4f;
                var pose=moving?CharacterPose.Run:age<ranged_window?CharacterPose.Attack:CharacterPose.Idle;
                if(!human) pose=ZombieAnimation.choose_pose(battle,i,Time.time);
                if(human&&battle.uses_melee(i))pose=moving?CharacterPose.MeleeRun:age<.55f&&battle.last_attack_melee[i]?CharacterPose.MeleeAttack:CharacterPose.MeleeIdle;
                float attack_window=human?(battle.uses_melee(i)?.55f:ranged_window):ZombieAnimation.attack_duration(battle.stats_for(i),battle.exploder[i]);
                float animation_age=Time.time+i*.137f;
                if(pose==CharacterPose.Run||pose==CharacterPose.MeleeRun)
                    animation_age=characters.locomotion_time(battle.stats_for(i).id,travelled[i],model_scale,animation_age);
                bool gate_outline=false;
                if(human)foreach(var building in battle.buildings)
                    if(building.health>0&&BattleSimulation.is_friendly_gate(building.label)&&building.bounds.Contains(battle.positions[i]+Vector3.up))gate_outline=true;
                characters.add(human,pose,pose==CharacterPose.Attack||pose==CharacterPose.MeleeAttack?age:animation_age,battle.positions[i],rotation,battle.exploder[i],model_scale,battle.stats_for(i).id,attack_window,gate_outline);
                if(human&&battle.stats_for(i).ammunition_type=="gunpowder"&&!battle.last_attack_melee[i]&&battle.attack_started_at[i]>seen_shots[i])
                { seen_shots[i]=battle.attack_started_at[i];effects.fire(characters.human_muzzle(battle.positions[i],rotation),rotation*Vector3.forward); }
            }
            foreach(var shot in battle.projectiles) if(shot.active&&(reveal||fog.is_visible(shot.position)))
            {
                Vector3 direction=battle.positions[shot.target]+Vector3.up-shot.position;
                if(battle.stats_for(shot.source).ammunition_type=="arrows"&&direction.sqrMagnitude>.0001f)
                    add_arrow(shot.position,Quaternion.LookRotation(direction));
                else add(2,shot.position,Vector3.one*.13f);
            }
            foreach(var shot in battle.defense_projectiles)if(reveal||fog.is_visible(shot.position))add(2,shot.position,Vector3.one*.3f);
            foreach(var shot in battle.enemy_projectiles)if(shot.active&&(reveal||fog.is_visible(shot.position)))add(0,shot.position,Vector3.one*.28f);
            foreach(var impact in battle.enemy_impacts)if(impact.expires>Time.time&&(reveal||fog.is_visible(impact.origin)))
                for(int i=0;i<32;i++)add(impact.acid?0:2,impact.origin+new Vector3(Mathf.Cos(i*Mathf.PI/16)*impact.radius,.12f,Mathf.Sin(i*Mathf.PI/16)*impact.radius),new Vector3(.18f,.08f,.18f));
            explosions.draw(battle,fog,reveal);
            characters.draw();
            equipment.draw(battle);flames.draw(battle,equipment);
            feedback.draw(battle,fog,reveal,Camera.main,equipment.selection_radius,equipment.head_height);
            for(int i=0;i<materials.Length;i++)
            {
                var parameters=new RenderParams(materials[i]){worldBounds=new Bounds(Vector3.zero,new Vector3(260,30,260)),shadowCastingMode=ShadowCastingMode.Off};
                for(int start=0;start<counts[i];start+=1023) Graphics.RenderMeshInstanced(parameters,cube,0,matrices[i],Math.Min(1023,counts[i]-start),start);
            }
        }
        private void add(int group,Vector3 point,Vector3 size)
        { if(counts[group]<matrices[group].Length)matrices[group][counts[group]++]=Matrix4x4.TRS(point,Quaternion.identity,size); }
        private void add_arrow(Vector3 point,Quaternion rotation)
        {
            if(counts[1]>=matrices[1].Length||counts[2]>=matrices[2].Length)return;
            matrices[1][counts[1]++]=Matrix4x4.TRS(point,rotation,new Vector3(.035f,.035f,.65f));
            matrices[2][counts[2]++]=Matrix4x4.TRS(point+rotation*new Vector3(0,0,-.24f),rotation,new Vector3(.13f,.03f,.14f));
        }
        public void Dispose() { characters.Dispose();equipment.Dispose();flames.Dispose();feedback.Dispose();effects.Dispose();explosions.Dispose();foreach(var material in materials)UnityEngine.Object.Destroy(material); }
    }
}
