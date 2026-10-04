using System;
using System.Collections.Generic;
using UnityEngine;
using ZombieGame.World;

namespace ZombieGame.FrontierTests
{
    /// <summary>Checks imported geometry against the Blender assembly, not fitted gameplay boxes.</summary>
    public static class CoastalAssemblyChecks
    {
        private const float GEOMETRY_TOLERANCE=.003f;

        public static void run(Transform parent)
        {
            var map=new FrontierMap(true,CoastalSessionConfig.load());
            var walls=new List<LandscapeRegion>();
            int piers=0,towers=0;
            float registration_error=0;
            foreach(var region in map.regions)
            {
                if(region.art_id==null)continue;
                require(region.art_authored_transform,"assembly region is not a legacy fitted object: "+region.art_id);
                var prefab=Resources.Load<GameObject>("CoastalBuildings/"+region.art_id);
                require(prefab!=null,"authored export exists: "+region.art_id);
                var placed=ImportedArchitecture.place(region,parent);
                check_import_transform(prefab,placed.transform.GetChild(0));
                var actual=measure_vertices(placed);
                var expected=region.art_expected_bounds;
                require(expected.size.x>0&&expected.size.y>0&&expected.size.z>0,"source geometry bounds recorded: "+region.art_id);
                float error=Mathf.Max(Vector3.Distance(actual.min,expected.min),Vector3.Distance(actual.max,expected.max));
                registration_error=Mathf.Max(registration_error,error);
                require(error<GEOMETRY_TOLERANCE,"source registration (no independent recenter, scale or grounding): "+region.art_id+
                    " error="+error+" actual="+actual+" source="+expected);
                if(region.label=="GATE TOWER")towers++;
                if(!region.label.Contains("WALL"))continue;
                walls.Add(region);if(region.label.Contains("END"))piers++;
                check_tower_replacement(region,map.regions);
            }
            require(walls.Count>=6&&piers==6&&towers==2,"three source wall runs, six original end piers and two authored towers");
            int joins=check_adjacent_sections(walls);
            require(joins>0,"adjacent original wall sections were tested");
            Debug.Log($"[CoastalAssembly] PASS source-world geometry, untouched FBX transforms, {joins} registered joins, two source towers; max_error={registration_error:F6}");
        }

        private static void check_import_transform(GameObject prefab,Transform actual)
        {
            var source_nodes=prefab.GetComponentsInChildren<Transform>();
            var actual_nodes=actual.GetComponentsInChildren<Transform>();
            require(source_nodes.Length==actual_nodes.Length,"assembly hierarchy retained");
            for(int index=0;index<source_nodes.Length;index++)
            {
                var source=source_nodes[index];var target=actual_nodes[index];
                require(Vector3.Distance(source.localPosition,target.localPosition)<.00001f&&
                    Vector3.Distance(source.localScale,target.localScale)<.00001f&&
                    Quaternion.Angle(source.localRotation,target.localRotation)<.001f,
                    "FBX child transform unchanged: "+source.name);
            }
        }

        private static Bounds measure_vertices(GameObject model)
        {
            // Renderer AABBs can grow under axis conversion. Inspect the actual imported surface.
            var bounds=new Bounds();bool has_vertex=false;
            foreach(var filter in model.GetComponentsInChildren<MeshFilter>())
                foreach(var vertex in filter.sharedMesh.vertices)
                {
                    var world=filter.transform.TransformPoint(vertex);
                    if(!has_vertex){bounds=new Bounds(world,Vector3.zero);has_vertex=true;}
                    else bounds.Encapsulate(world);
                }
            require(has_vertex,"authored assembly contains readable mesh vertices");
            return bounds;
        }

        private static void check_tower_replacement(LandscapeRegion wall,List<LandscapeRegion> regions)
        {
            if(wall.art_sideways)return;
            foreach(var tower in regions)
            {
                if(tower.label!="GATE TOWER")continue;
                require(tower.art_wall_contact_width>0&&tower.art_wall_contact_width<=tower.bounds.size.x,
                    "source tower contact width recorded independently of decorative overhangs: "+tower.art_id);
                float half_width=tower.art_wall_contact_width*.5f;
                float overlap=Mathf.Min(wall.art_expected_bounds.max.x,tower.bounds.center.x+half_width)-
                    Mathf.Max(wall.art_expected_bounds.min.x,tower.bounds.center.x-half_width);
                require(overlap<GEOMETRY_TOLERANCE,"source wall is removed from tower interval: "+wall.art_id);
            }
        }

        private static int check_adjacent_sections(List<LandscapeRegion> walls)
        {
            int joins=0;
            for(int first=0;first<walls.Count;first++)
                for(int second=first+1;second<walls.Count;second++)
                {
                    var a=walls[first];var b=walls[second];
                    if(a.art_sideways!=b.art_sideways)continue;
                    int axis=a.art_sideways?2:0,transverse=a.art_sideways?0:2;
                    if(Mathf.Abs(a.art_position[transverse]-b.art_position[transverse])>.001f)continue;
                    // Navigation boxes can be padded to cell boundaries. The source surface is the seam.
                    if(Mathf.Abs(a.art_expected_bounds.max[axis]-b.art_expected_bounds.min[axis])>GEOMETRY_TOLERANCE&&
                        Mathf.Abs(b.art_expected_bounds.max[axis]-a.art_expected_bounds.min[axis])>GEOMETRY_TOLERANCE)continue;
                    require(Mathf.Abs(a.art_position[transverse]-b.art_position[transverse])<.001f&&
                        Mathf.Abs(a.art_position.y-b.art_position.y)<.001f&&Mathf.Abs(a.art_scale-b.art_scale)<.00001f&&
                        Mathf.Abs(Mathf.DeltaAngle(a.art_yaw,b.art_yaw))<.001f,"shared source cross-section registration: "+a.art_id+" / "+b.art_id);
                    joins++;
                }
            return joins;
        }

        private static void require(bool condition,string message)
        {if(!condition)throw new InvalidOperationException("[CoastalAssembly] FAIL "+message);}
    }
}
