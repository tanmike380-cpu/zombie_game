using System;
using System.Collections.Generic;
using UnityEngine;

namespace ZombieGame.World
{
    public enum LandscapeKind { River, Forest, Cliff, Building }
    public sealed class ResourceSite
    {
        public readonly string kind;
        public readonly Vector3 position;
        public bool claimed;
        public ResourceSite(string kind,float x,float z,bool claimed)
        {this.kind=kind;position=new Vector3(x,0,z);this.claimed=claimed;}
    }
    public struct LandscapeRegion
    {
        public Bounds bounds;
        public LandscapeKind kind;
        public string label;
        public LandscapeRegion(float x, float z, float width, float depth, float height, LandscapeKind kind, string label = "")
        { bounds = new Bounds(new Vector3(x,height*.5f,z),new Vector3(width,height,depth)); this.kind=kind; this.label=label; }
    }

    /// <summary>Deterministic 256-tile frontier. Visual regions and navigation blockers share one geometry source.</summary>
    public sealed class FrontierMap
    {
        public const int SIZE = 256, SOLDIERS = 400, ZOMBIES = 5000;
        public readonly List<LandscapeRegion> regions = new List<LandscapeRegion>();
        public readonly Vector3 base_center = new Vector3(-92,0,-92);
        public readonly Vector3[] spawns = new Vector3[SOLDIERS+ZOMBIES];
        public readonly bool[] explosive = new bool[SOLDIERS+ZOMBIES];
        public Bounds[] blockers;
        public readonly ResourceSite[] resource_sites={
            new ResourceSite("Food",-111,-112,true),new ResourceSite("Wood",-111,-64,true),
            new ResourceSite("Stone",-118,-44,true),new ResourceSite("Iron",-61,-116,true),
            new ResourceSite("Iron",44,-101,false),new ResourceSite("Stone",79,60,false),
            new ResourceSite("Food",-80,80,false),new ResourceSite("Wood",-75,45,false)};

        public FrontierMap()
        {
            // A connected meandering river stops short of the north edge: no bridge or invisible crossing.
            for (int z=-128;z<=76;z+=4)
                regions.Add(new LandscapeRegion(-24+Mathf.Sin(z*.035f)*12,z,13,4.05f,.35f,LandscapeKind.River));
            add_forest(-89,-40,26,16); add_forest(-51,-91,12,26);
            add_forest(-94,26,30,22); add_forest(-49,70,24,24);
            add_forest(38,-73,26,24); add_forest(69,-18,30,24); add_forest(27,38,22,28);
            add_forest(92,91,25,28); add_forest(-100,104,25,18);
            regions.Add(new LandscapeRegion(-52,-14,20,18,5,LandscapeKind.Cliff));
            regions.Add(new LandscapeRegion(46,76,32,15,6,LandscapeKind.Cliff));
            regions.Add(new LandscapeRegion(107,20,20,40,5,LandscapeKind.Cliff));
            add_building(-104,-103,9,7,"COMMAND HALL");
            add_building(-87,-108,7,5,"GRANARY");
            add_building(-68,-105,8,6,"POWDER WORKS");
            add_building(-106,-85,7,6,"BARRACKS");
            add_building(-91,-94,5,4,"HOUSE"); add_building(-82,-94,5,4,"HOUSE");
            add_building(-73,-94,5,4,"ARROW WORKS"); add_building(-111,-64,6,5,"LUMBER CAMP");
            blockers = regions.ConvertAll(region=>region.bounds).ToArray();
            spawn_units();
        }

        private void add_building(float x,float z,float width,float depth,string name)
        { regions.Add(new LandscapeRegion(x,z,width,depth,3,LandscapeKind.Building,name)); }

        private void add_forest(float x,float z,float width,float depth)
        {
            for(float dz=-depth/2;dz<depth/2;dz+=4)
                for(float dx=-width/2;dx<width/2;dx+=4)
                    if(dx*dx/(width*width*.25f)+dz*dz/(depth*depth*.25f)<1)
                        regions.Add(new LandscapeRegion(x+dx,z+dz,4.05f,4.05f,4,LandscapeKind.Forest));
        }

        public bool blocked(Vector3 point,float clearance = .7f)
        {
            if(Mathf.Abs(point.x)>127-clearance || Mathf.Abs(point.z)>127-clearance) return true;
            foreach(var region in regions)
                if(point.x>region.bounds.min.x-clearance && point.x<region.bounds.max.x+clearance &&
                   point.z>region.bounds.min.z-clearance && point.z<region.bounds.max.z+clearance) return true;
            return false;
        }

        private void spawn_units()
        {
            for(int i=0;i<SOLDIERS;i++) spawns[i]=new Vector3(-102+i%20*.85f,0,-79+i/20*.85f);
            int filled=SOLDIERS;
            var random=new System.Random(92026);
            for(int attempt=0;filled<spawns.Length && attempt<200000;attempt++)
            {
                var point=new Vector3(-122+(float)random.NextDouble()*244,0,-122+(float)random.NextDouble()*244);
                if(point.x < -40 && point.z < -48) continue; // Established settlement, not a spawn trap.
                if(blocked(point,1)) continue;
                // A spatial occupancy grid below keeps native agents from spawning on top of one another.
                int x=Mathf.FloorToInt(point.x+128), z=Mathf.FloorToInt(point.z+128);
                if(spawn_cells[x+z*256]) continue;
                spawn_cells[x+z*256]=true;
                spawns[filled]=new Vector3(x-127.5f,0,z-127.5f);
                if(blocked(spawns[filled],.65f)) continue;
                explosive[filled]=(filled-SOLDIERS)%12==0;
                filled++;
            }
            if(filled!=spawns.Length) throw new InvalidOperationException("Frontier zombie spawn capacity exhausted");
            foreach(var point in spawns) if(blocked(point,.3f)) throw new InvalidOperationException("Blocked frontier spawn: "+point);
        }
        private readonly bool[] spawn_cells=new bool[256*256];
    }
}
