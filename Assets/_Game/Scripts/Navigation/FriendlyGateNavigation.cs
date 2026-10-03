using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace ZombieGame.Navigation
{
    public partial class NativeNavMeshCrowd
    {
        private readonly Dictionary<Bounds,int> gate_areas=new Dictionary<Bounds,int>();
        private int gate_area_mask;

        /// <summary>Native area filtering blocks enemies only; no teleports or off-mesh traversal.</summary>
        private void append_gate_areas(List<NavMeshBuildSource> sources,Bounds[] gates)
        {
            if(gates==null)return;
            if(gates.Length>28)throw new ArgumentException("Scenario exceeds distinct gate area capacity");
            for(int i=0;i<gates.Length;i++)
            {
                int area=3+i;gate_areas.Add(gates[i],area);gate_area_mask|=1<<area;
                var volume=gates[i];volume.Expand(new Vector3(.4f,4,.4f));
                sources.Add(new NavMeshBuildSource{shape=NavMeshBuildSourceShape.ModifierBox,
                    transform=Matrix4x4.TRS(volume.center,Quaternion.identity,Vector3.one),size=volume.size,area=area});
            }
        }

        public GameObject add_friendly_gate(Bounds bounds)
        {
            if(!gate_areas.ContainsKey(bounds))throw new ArgumentException("Gate was not included in the navigation bake");
            var root_object=new GameObject("Friendly gate corridor");root_object.transform.SetParent(root.transform,false);
            // Same 72%-wide centre corridor as the accepted Blender preview, scaled with the original tower.
            float jamb=bounds.size.x*.14f;
            foreach(float side in new[]{-1f,1f})
            {
                var blocker=new GameObject("Gate side pier");blocker.transform.SetParent(root_object.transform,false);
                blocker.transform.position=bounds.center+Vector3.right*side*(bounds.extents.x-jamb*.5f);
                var carving=blocker.AddComponent<NavMeshObstacle>();carving.shape=NavMeshObstacleShape.Box;
                carving.size=new Vector3(jamb,bounds.size.y,bounds.size.z);carving.carving=true;carving.carveOnlyStationary=false;
            }
            var updated=new List<Bounds>(walls){bounds};walls=updated.ToArray();return root_object;
        }

        private void open_destroyed_gate(Bounds bounds)
        {
            if(!gate_areas.TryGetValue(bounds,out int area))return;
            foreach(var agent in agents)agent.areaMask|=1<<area;
            gate_areas.Remove(bounds);
        }
    }
}
