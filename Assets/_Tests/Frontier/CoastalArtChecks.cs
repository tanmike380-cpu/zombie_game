using System;
using UnityEngine;
using ZombieGame.Presentation;
using ZombieGame.World;
using ZombieGame.Balance;

namespace ZombieGame.FrontierTests
{
    /// <summary>Art-space checks only; never edits gameplay balance or agent movement.</summary>
    public static class CoastalArtChecks
    {
        public static void run()
        {
            var hound=Resources.Load<CharacterFrames>("CoastalUnits/Hound");
            require(hound!=null&&hound.locomotion_stride>.1f,"authored hound stride imported");
            foreach(var pose in hound.poses)
                foreach(var mesh in pose.frames)
                    require(Mathf.Abs(mesh.bounds.min.y)<.045f,"hound feet remain grounded in every baked pose: "+mesh.name+" "+mesh.bounds.min.y);
            var run=hound.poses[(int)CharacterPose.Run];
            var first=run.frames[0].vertices;var opposite=run.frames[run.frames.Length/2].vertices;
            float maximum=0;for(int i=0;i<first.Length;i++)maximum=Mathf.Max(maximum,Vector3.Distance(first[i],opposite[i]));
            require(maximum>.05f,"hound locomotion must actually articulate limbs");
            var parent=new GameObject("Disposable art scale check");
            try
            {
                var region=new LandscapeRegion(0,0,18,5,3,LandscapeKind.Building,"FORTRESS WALL");
                var wall=ImportedArchitecture.place(region,parent.transform);
                var renderers=wall.GetComponentsInChildren<Renderer>();var bounds=renderers[0].bounds;
                foreach(var renderer in renderers)bounds.Encapsulate(renderer.bounds);
                var giant=Resources.Load<CharacterFrames>("CoastalUnits/Headbutter");
                float giant_height=giant.poses[0].frames[0].bounds.size.y*UnitBalance.get("giant").model_scale;
                require(bounds.size.y>giant_height&&bounds.size.y<giant_height*1.5f,"whole original wall is taller than headbutter, not a stretched shaft");
                require(bounds.size.x<=18.01f&&bounds.size.z<=5.01f,"wall geometry fits its enlarged navigation footprint");
                var side_wall=ImportedArchitecture.place(new LandscapeRegion(30,0,5,18,3,LandscapeKind.Building,"FORTRESS WALL SIDE"),parent.transform);
                var side_renderers=side_wall.GetComponentsInChildren<Renderer>();var side_bounds=side_renderers[0].bounds;
                foreach(var renderer in side_renderers)side_bounds.Encapsulate(renderer.bounds);
                require(Mathf.Abs(side_bounds.size.y-bounds.size.y)<.01f,"side wall retains front wall height and imported up axis");
                require(Mathf.Abs(side_bounds.size.z-bounds.size.x)<.01f&&Mathf.Abs(side_bounds.size.x-bounds.size.z)<.01f,"side wall fills the same span after yaw without visual gaps");
                require(ImportedArchitecture.maximum_aspect_error<.001f,"all source axes uniformly scaled");
                require(Resources.Load<Shader>("Effects/GreekFlame")!=null,"flame shader included in player");
                require(Mathf.Abs(FlameEffects.jet_length(Vector3.zero,new Vector3(0,2,1),Vector3.forward,6)-5)<.001f,"muzzle offset never adds fake weapon range");
                Debug.Log($"[CoastalArt] PASS grounded hound, stride={hound.locomotion_stride:F3}, wall={bounds.size.y:F2}, headbutter={giant_height:F2}, original proportions, flame resource");
            }
            finally{UnityEngine.Object.Destroy(parent);}
        }
        private static void require(bool condition,string message)
        {if(!condition)throw new InvalidOperationException("[CoastalArt] FAIL "+message);}
    }
}
