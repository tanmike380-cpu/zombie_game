using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace ZombieGame.Navigation
{
    public partial class NativeNavMeshCrowd
    {
        private readonly Dictionary<float,int> large_types=new Dictionary<float,int>();
        private readonly List<NavMeshData> large_data=new List<NavMeshData>();
        private readonly List<NavMeshDataInstance> large_instances=new List<NavMeshDataInstance>();

        /// <summary>Native clearance-aware routes for large zombies, not just enlarged visual meshes.
        /// Profiles share the same obstacle sources and runtime carving.</summary>
        private int ensure_large_profile(float radius,List<NavMeshBuildSource> sources)
        {
            if(large_types.TryGetValue(radius,out int existing))return existing;
            var settings=NavMesh.CreateSettings();settings.agentRadius=radius;settings.agentHeight=1.2f;
            settings.agentClimb=.2f;settings.overrideVoxelSize=true;settings.voxelSize=.1f;
            var mesh=NavMeshBuilder.BuildNavMeshData(settings,sources,new Bounds(Vector3.zero,new Vector3(260,10,260)),Vector3.zero,Quaternion.identity);
            if(mesh==null){NavMesh.RemoveSettings(settings.agentTypeID);throw new InvalidOperationException("Large-unit navigation bake failed, radius="+radius);}
            large_types.Add(radius,settings.agentTypeID);large_data.Add(mesh);large_instances.Add(NavMesh.AddNavMeshData(mesh));
            return settings.agentTypeID;
        }
        private void dispose_large_profiles()
        {
            foreach(var instance in large_instances)if(instance.valid)instance.Remove();
            foreach(var mesh in large_data)if(mesh!=null)UnityEngine.Object.Destroy(mesh);
            foreach(int agent_type in large_types.Values)NavMesh.RemoveSettings(agent_type);
            large_instances.Clear();large_data.Clear();large_types.Clear();
        }
    }
}
