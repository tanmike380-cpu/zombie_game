using UnityEngine;

namespace ZombieGame.Combat
{
    public partial class BattleSimulation
    {
        private bool[] siege_committed;
        private Vector3 siege_goal;
        private BattleBuilding siege_headquarters;
        public int siege_units {get;private set;}
        /// <summary>Persistent attack-move objective. Local combat may interrupt it; sound cannot replace it.</summary>
        public int order_siege(Vector3 goal,int count)
        {
            siege_committed??=new bool[total_count];siege_goal=goal;
            foreach(var building in buildings)
                if(building.headquarters&&building.health>0&&!building.infected){siege_headquarters=building;siege_goal=building.bounds.center;siege_goal.y=0;break;}
            int ordered=0;
            for(int i=soldier_count;i<total_count&&ordered<count;i++)
            {
                if(health[i]<=0||siege_committed[i])continue;
                siege_committed[i]=true;ordered++;siege_units++;
                if(!activated[i]){activated[i]=true;ever_active++;}
                memories[i]=siege_goal;path_target[i]=-1;needs_path[i]=true;
            }
            return ordered;
        }
        private bool has_siege_order(int index)=>siege_committed!=null&&siege_committed[index];
    }
}
