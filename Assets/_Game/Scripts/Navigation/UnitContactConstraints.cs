using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using ZombieGame.Balance;

namespace ZombieGame.Navigation
{
    /// <summary>Accept only the non-penetrating part of native navigation movement.
    /// Swept discs prevent tunnelling from valid non-overlapping spawns; no destinations, paths,
    /// commands or attack timers are changed. Deployment owns clearance checks, not this solver.</summary>
    public sealed class UnitContactConstraints : IComparer<int>
    {
        private const float SKIN = .001f;
        private readonly NativeNavMeshCrowd crowd;
        private readonly Vector3[] accepted, proposed;
        private readonly float[] radii, progress;
        private readonly bool[] present, zombies;
        private readonly int[] heads, next, previous, cells, order;
        private readonly float cell_size;
        private readonly int side;
        private int frame;
        public int blocked_moves {get;private set;}
        public double solve_ms {get;private set;}

        public UnitContactConstraints(NativeNavMeshCrowd crowd, UnitStats[] stats)
        {
            this.crowd=crowd;int count=stats.Length;
            accepted=new Vector3[count];proposed=new Vector3[count];radii=new float[count];progress=new float[count];
            present=new bool[count];zombies=new bool[count];next=new int[count];previous=new int[count];cells=new int[count];order=new int[count];
            float maximum=0;
            for(int i=0;i<count;i++){radii[i]=UnitBalance.navigation_radius(stats[i]);zombies[i]=UnitBalance.is_zombie(stats[i].id);maximum=Mathf.Max(maximum,radii[i]);}
            cell_size=maximum*2+SKIN*2;side=Mathf.CeilToInt(260/cell_size);
            heads=new int[side*side];
        }

        public void resolve(float[] health)
        {
            long started=System.Diagnostics.Stopwatch.GetTimestamp();blocked_moves=0;
            Array.Fill(heads,-1);int count=0;
            for(int i=0;i<accepted.Length;i++)
            {
                if(health[i]<=0){present[i]=false;continue;}
                var agent=crowd.agents[i];Vector3 point=crowd.transforms[i].position;
                if(!present[i]){accepted[i]=point;present[i]=true;}
                // Human-to-human avoidance is deliberately unchanged. Native displacement of
                // stopped humans remains valid unless it would penetrate a living zombie.
                proposed[i]=agent.enabled && agent.isOnNavMesh && (!agent.isStopped || !zombies[i]) ? agent.nextPosition : accepted[i];
                Vector3 motion=proposed[i]-accepted[i];motion.y=0;
                Vector3 direction=agent.enabled && agent.isOnNavMesh && agent.hasPath ? agent.steeringTarget-accepted[i] : motion;
                direction.y=0;
                // Process the front of co-moving chains first, not a permanent unit-index priority.
                progress[i]=motion.sqrMagnitude>.000001f ? Vector3.Dot(accepted[i],direction.normalized) : float.NegativeInfinity;
                order[count++]=i;insert(i);
            }
            Array.Sort(order,0,count,this);frame++;
            for(int slot=0;slot<count;slot++)accept_native_step(order[slot]);
            solve_ms=(System.Diagnostics.Stopwatch.GetTimestamp()-started)*1000.0/System.Diagnostics.Stopwatch.Frequency;
        }

        public int Compare(int left,int right)
        {
            int comparison=progress[right].CompareTo(progress[left]);
            return comparison!=0?comparison:((left+frame)%order.Length).CompareTo((right+frame)%order.Length);
        }

        /// <summary>Explicit disposable-test relocation; never use to bypass contact during gameplay.
        /// The owning test must reset or dispose its simulation after the fixture.</summary>
        public bool warp_fixture(int index, Vector3 position)
        {
            if(!crowd.agents[index].Warp(position))return false;
            accepted[index]=crowd.agents[index].nextPosition;
            present[index]=true;
            return true;
        }

        private void accept_native_step(int index)
        {
            var agent=crowd.agents[index];
            if(!agent.enabled || !agent.isOnNavMesh)return;
            Vector3 start=accepted[index], end=proposed[index];end.y=start.y;
            var filter=new NavMeshQueryFilter {agentTypeID=agent.agentTypeID,areaMask=agent.areaMask};
            Vector3 motion=end-start;motion.y=0;
            Vector3 result=start;
            bool blocked=false;
            // Keep the tangential portion of native avoidance at a contact instead of freezing
            // an entire co-moving chain. Each slide is swept again against bodies and terrain.
            for(int iteration=0;iteration<3 && motion.sqrMagnitude>.00000001f;iteration++)
            {
                float fraction=safe_fraction(index,result,motion,out var normal);
                // An untouched native step already has a navigable endpoint. Query terrain only
                // when a body clips it or a slide changes its direction.
                if((fraction<1 || iteration>0) && NavMesh.Raycast(result,result+motion,out var edge,filter))
                {
                    float terrain_fraction=Mathf.Clamp01(Vector3.Dot(edge.position-result,motion)/motion.sqrMagnitude);
                    if(terrain_fraction<=fraction)
                    {
                        fraction=terrain_fraction;normal=edge.normal;normal.y=0;normal.Normalize();
                        if(Vector3.Dot(normal,motion)>0)normal=-normal;
                    }
                }
                result+=motion*fraction;
                if(fraction>=.9999f)break;
                blocked=true;
                motion*=1-fraction;
                motion-=normal*Mathf.Min(0,Vector3.Dot(motion,normal));
            }
            if(blocked)blocked_moves++;
            // Keep Unity's internal simulation and the visible transform at the same accepted position.
            // nextPosition stays on connected NavMesh, unlike a warp or direct off-mesh displacement.
            agent.nextPosition=result;
            result=agent.nextPosition;
            crowd.transforms[index].position=result;
            remove(index);accepted[index]=result;insert(index);
        }

        private float safe_fraction(int index,Vector3 start,Vector3 motion,out Vector3 normal)
        {
            normal=Vector3.zero;
            float length_squared=motion.x*motion.x+motion.z*motion.z;
            if(length_squared<.00000001f)return 1;
            float fraction=1;
            float reach=cell_size;
            int min_x=coordinate(Mathf.Min(start.x,start.x+motion.x)-reach),max_x=coordinate(Mathf.Max(start.x,start.x+motion.x)+reach);
            int min_z=coordinate(Mathf.Min(start.z,start.z+motion.z)-reach),max_z=coordinate(Mathf.Max(start.z,start.z+motion.z)+reach);
            for(int z=min_z;z<=max_z;z++)for(int x=min_x;x<=max_x;x++)
                for(int other=heads[x+z*side];other>=0;other=next[other])
                {
                    if(other==index || (!zombies[index] && !zombies[other]))continue;
                    Vector3 offset=start-accepted[other];offset.y=0;
                    float radius=radii[index]+radii[other];
                    float toward=Vector3.Dot(offset,motion);
                    if(toward>=-.0000001f)continue;
                    float gap=offset.sqrMagnitude-(radius+SKIN)*(radius+SKIN);
                    if(gap<=0){fraction=0;normal=offset.normalized;continue;}
                    float discriminant=toward*toward-length_squared*gap;
                    if(discriminant<=0)continue;
                    float hit=(-toward-Mathf.Sqrt(discriminant))/length_squared;
                    if(hit>=0 && hit<fraction){fraction=hit;normal=(offset+motion*hit).normalized;}
                }
            return fraction;
        }

        private int coordinate(float value)=>Mathf.Clamp(Mathf.FloorToInt((value+130)/cell_size),0,side-1);
        private void insert(int index)
        {
            int key=coordinate(accepted[index].x)+coordinate(accepted[index].z)*side;
            cells[index]=key;previous[index]=-1;next[index]=heads[key];
            if(next[index]>=0)previous[next[index]]=index;
            heads[key]=index;
        }
        private void remove(int index)
        {
            if(previous[index]>=0)next[previous[index]]=next[index];else heads[cells[index]]=next[index];
            if(next[index]>=0)previous[next[index]]=previous[index];
        }
    }
}
