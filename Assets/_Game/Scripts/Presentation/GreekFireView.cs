using System;
using System.Collections.Generic;
using UnityEngine;
using ZombieGame.Combat;

namespace ZombieGame.Presentation
{
    /// <summary>Original rigid-weighted turret and wheels. All fitting is uniform; no bone scaling.</summary>
    public sealed class GreekFireView:IDisposable
    {
        private sealed class Vehicle
        {
            public GameObject root;
            public Transform turret;
            public Transform[] wheels,axles;
            public Transform pivot,muzzle;
            public Vector3 turret_position,muzzle_position;
            public float yaw;
            public Quaternion turret_rotation;
            public Vector3 previous_position;
        }
        private readonly Dictionary<int,Vehicle> vehicles=new Dictionary<int,Vehicle>();
        private readonly Transform parent;
        public GreekFireView(Transform parent){this.parent=parent;}
        private Vehicle create_vehicle(int index,Vector3 position)
        {
            var prefab=Resources.Load<GameObject>("CoastalBuildings/GreekFire");
            if(prefab==null)throw new InvalidOperationException("Missing Greek fire source prefab");
            var root=new GameObject("Controllable Greek fire "+index);root.transform.SetParent(parent,false);
            var model=UnityEngine.Object.Instantiate(prefab,root.transform);
            foreach(var animator in model.GetComponentsInChildren<Animator>())animator.enabled=false;
            var transforms=model.GetComponentsInChildren<Transform>();
            Transform muzzle=Array.Find(transforms,bone=>bone.name=="Muzzle"),aim=Array.Find(transforms,bone=>bone.name=="MuzzleForward");
            if(muzzle==null||aim==null)throw new InvalidOperationException("Greek fire requires source-space muzzle markers");
            Vector3 authored_forward=aim.position-muzzle.position;authored_forward.y=0;
            model.transform.rotation=Quaternion.FromToRotation(authored_forward,Vector3.forward)*model.transform.rotation;
            var renderers=model.GetComponentsInChildren<Renderer>();
            var bounds=renderers[0].bounds;foreach(var renderer in renderers)bounds.Encapsulate(renderer.bounds);
            model.transform.localScale*=3.9f/bounds.size.y;
            bounds=renderers[0].bounds;foreach(var renderer in renderers)bounds.Encapsulate(renderer.bounds);
            model.transform.position-=new Vector3(bounds.center.x,bounds.min.y,bounds.center.z);
            Transform turret=null;var wheels=new List<Transform>();var axles=new List<Transform>();
            foreach(var bone in model.GetComponentsInChildren<Transform>())
            {
                if(bone.name.EndsWith("Head_0"))turret=bone;
                if(bone.name=="bone_12"||bone.name=="bone_13"||bone.name=="bone_14"||bone.name=="bone_22")
                {wheels.Add(bone);axles.Add(Array.Find(transforms,item=>item.name=="Axle_"+bone.name));}
            }
            if(turret==null)throw new InvalidOperationException("Greek fire turret bone missing");
            var vehicle=new Vehicle{root=root,turret=turret,wheels=wheels.ToArray(),axles=axles.ToArray(),turret_position=turret.localPosition,
                turret_rotation=turret.localRotation,pivot=Array.Find(transforms,item=>item.name=="TurretPivot"),muzzle=muzzle,previous_position=position};
            vehicles.Add(index,vehicle);return vehicle;
        }
        public Vector3 muzzle(int source,Vector3 fallback)
        {
            return vehicles.TryGetValue(source,out var vehicle)?vehicle.pivot.position+Quaternion.AngleAxis(vehicle.yaw,Vector3.up)*(vehicle.muzzle.position-vehicle.pivot.position):fallback+Vector3.up*1.6f;
        }
        public void draw(BattleSimulation battle)
        {
            for(int i=0;i<battle.soldier_count;i++)
            {
                if(battle.stats_for(i).id!="greek_fire"||battle.is_reserve(i))continue;
                if(!vehicles.TryGetValue(i,out var vehicle))vehicle=create_vehicle(i,battle.positions[i]);
                vehicle.root.SetActive(battle.health[i]>0);if(battle.health[i]<=0)continue;
                Vector3 motion=battle.positions[i]-vehicle.previous_position;
                vehicle.previous_position=battle.positions[i];vehicle.root.transform.position=battle.positions[i];
                if(motion.sqrMagnitude>.0001f)vehicle.root.transform.rotation=Quaternion.RotateTowards(vehicle.root.transform.rotation,Quaternion.LookRotation(motion),150*Time.deltaTime);
                Vector3 direction=battle.soldier_facing[i];direction.y=0;
                if(direction.sqrMagnitude<.001f)direction=vehicle.root.transform.forward;
                float yaw=Vector3.SignedAngle(vehicle.root.transform.forward,direction,Vector3.up);vehicle.yaw=yaw;
                vehicle.turret.localPosition=vehicle.turret_position;vehicle.turret.localRotation=vehicle.turret_rotation;
                vehicle.turret.RotateAround(vehicle.pivot.position,Vector3.up,yaw);
                for(int wheel=0;wheel<vehicle.wheels.Length;wheel++)vehicle.wheels[wheel].RotateAround(vehicle.axles[wheel].position,vehicle.root.transform.right,-motion.magnitude*150);
            }
        }
        public void Dispose(){foreach(var vehicle in vehicles.Values)UnityEngine.Object.Destroy(vehicle.root);}
    }
}
