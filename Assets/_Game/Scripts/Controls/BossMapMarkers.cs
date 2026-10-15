using System;
using UnityEngine;
using ZombieGame.Combat;

namespace ZombieGame.Controls
{
    /// <summary>Draw only living, explicitly public boss positions into the 256-tile minimap.
    /// This layer has no access to visibility/discovery state and cannot reveal nearby enemies.</summary>
    public static class BossMapMarkers
    {
        public static void draw(BattleSimulation battle,Color32[] pixels)
        {draw(battle,pixels,new Rect(-128,-128,256,256));}

        public static void draw(BattleSimulation battle,Color32[] pixels,Rect bounds)
        {
            if(pixels==null||pixels.Length!=256*256)throw new ArgumentException("Boss markers require a 256-tile map buffer");
            for(int i=battle.soldier_count;i<battle.total_count;i++)
            {
                if(battle.health[i]<=0||!battle.stats_for(i).map_revealed)continue;
                Vector3 p=battle.positions[i];
                if(!bounds.Contains(new Vector2(p.x,p.z)))continue;
                int cx=Mathf.FloorToInt((p.x-bounds.xMin)/bounds.width*256),cz=Mathf.FloorToInt((p.z-bounds.yMin)/bounds.height*256);
                for(int z=-4;z<=4;z++)for(int x=-4;x<=4;x++)
                    if(Mathf.Abs(x)+Mathf.Abs(z)<=4&&cx+x>=0&&cx+x<256&&cz+z>=0&&cz+z<256)
                        pixels[cx+x+(cz+z)*256]=Mathf.Abs(x)+Mathf.Abs(z)>=3?new Color32(255,205,55,255):new Color32(180,25,55,255);
            }
        }
    }
}
