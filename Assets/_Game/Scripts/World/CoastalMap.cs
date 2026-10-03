using System;
using System.Collections.Generic;
using UnityEngine;
using ZombieGame.Balance;

namespace ZombieGame.World
{
    public sealed partial class FrontierMap
    {
        /// <summary>Scenario layout only: sea behind camp, destructible walls on three land sides.</summary>
        private void build_coastal_regions()
        {
            regions.Add(new LandscapeRegion(0,-121,256,14,.35f,LandscapeKind.River,"COASTAL SEA"));
            add_building(-104,-103,12,9,"COMMAND HALL");
            add_building(-83,-104,10,9,"PASTURE");
            add_building(-67,-104,8,7,"LUMBER CAMP");
            add_building(-117,-93,5,5,"GRANARY");
            add_building(-99,-94,5,4,"AMMUNITION DEPOT");
            add_building(-82,-91,6,5,"ARROW WORKS");
            add_building(-69,-92,6,5,"POWDER WORKS");
            add_building(-113,-83,6,5,"BARRACKS");
            add_building(-107,-79,2,2,"OUTPOST");
            add_building(-86,-80,2,2,"OUTPOST");
            add_building(-65,-80,2,2,"OUTPOST");
            for(int x=-123;x<=-57;x+=2)
                add_building(x,-55,2,2,x==-123||x==-57?"WALL PIER":x==-99||x==-79?"GATE TOWER":"STONE WALL");
            for(int z=-113;z<-55;z+=2)
            {
                add_building(-123,z,2,2,"STONE WALL SIDE");
                add_building(-57,z,2,2,z==-83?"FIRE TOWER":"STONE WALL SIDE");
            }
            add_forest(-40,5,20,24);add_forest(35,-55,20,22);
            add_forest(55,60,28,20);add_forest(-90,85,26,22);
            regions.Add(new LandscapeRegion(90,12,20,24,5,LandscapeKind.Cliff));
        }

        private void spawn_coastal_units()
        {
            float spacing=UnitBalance.config.formation_spacing;
            int count=4;
            for(int i=0;i<4;i++){spawns[i]=new Vector3(-112+i*15,0,-59);unit_ids[i]="greek_fire";}
            for(float z=-63;z>-106&&count<HUMAN_CAPACITY;z-=spacing)
                for(float x=-119;x<-60&&count<HUMAN_CAPACITY;x+=spacing)
                {
                    var point=new Vector3(x,0,z);
                    if(blocked(point,UnitBalance.config.unit_navigation_radius+.15f))continue;
                    spawns[count]=point;unit_ids[count]=count<initial_humans*.8f?"heavy_crossbowman":"archer";count++;
                }
            if(count!=HUMAN_CAPACITY)throw new InvalidOperationException("Coastal muster cannot fit shared unit spacing");
            var cells=new List<Vector3>();
            float gap=UnitBalance.config.zombie_navigation_radius*2+.08f;
            int row=0;
            for(float z=imported_only?-24:-37;z<(imported_only?55:123);z+=gap*.8660254f,row++)
                for(float x=-120+(row%2)*gap*.5f;x<(imported_only?-51:123);x+=gap)
                {var point=new Vector3(x,0,z);if(!blocked(point,1))cells.Add(point);}
            var random=new System.Random(20261002);
            for(int i=0;i<cells.Count;i++){int swap=random.Next(i,cells.Count);var point=cells[i];cells[i]=cells[swap];cells[swap]=point;}
            int population=spawns.Length-HUMAN_CAPACITY;
            for(int i=0;i<population;i++)
            {
                int index=HUMAN_CAPACITY+i;
                spawns[index]=cells[i];
                unit_ids[index]=i<population*.6f?"walker":i<population*.9f?"exploder":i<population-10?"zombie_hound":i<population-2?"giant":"boss";
                explosive[index]=unit_ids[index]=="exploder";
            }
        }
    }
}
