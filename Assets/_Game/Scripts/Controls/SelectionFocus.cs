using UnityEngine;

namespace ZombieGame.Controls
{
    /// <summary>Camera-only density estimate. Focus lands on a living unit in the largest local cluster.</summary>
    public static class SelectionFocus
    {
        public static Vector3 find(Vector3[] positions,float[] health,bool[] selected,int count)
        {
            int best=-1,best_neighbors=-1;float best_spread=float.PositiveInfinity;
            const float CLUSTER_RADIUS_SQUARED=64;
            bool has_selection=false;
            for(int i=0;i<count;i++)if(health[i]>0&&selected[i]){has_selection=true;break;}
            for(int i=0;i<count;i++)
            {
                if(health[i]<=0||(has_selection&&!selected[i]))continue;
                int neighbors=0;float spread=0;
                for(int j=0;j<count;j++)
                {
                    if(health[j]<=0||(has_selection&&!selected[j]))continue;
                    float distance=(positions[j]-positions[i]).sqrMagnitude;
                    if(distance>CLUSTER_RADIUS_SQUARED)continue;
                    neighbors++;spread+=distance;
                }
                if(neighbors>best_neighbors||(neighbors==best_neighbors&&spread<best_spread))
                {best=i;best_neighbors=neighbors;best_spread=spread;}
            }
            return best>=0?new Vector3(positions[best].x,0,positions[best].z):Vector3.zero;
        }
    }
}
