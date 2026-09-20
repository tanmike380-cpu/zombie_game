using System;
using UnityEngine;
using ZombieGame.Presentation;

namespace ZombieGame.CharacterTests
{
    public static class CharacterVisualChecks
    {
        public static void run()
        {
            CharacterFrames basic = null, explosive = null;
            foreach(string name in new[] { "Human","Zombie","Exploder" })
            {
                var asset=Resources.Load<CharacterFrames>("CharacterGenerated/"+name);
                require(asset!=null && asset.material!=null && asset.material.shader.isSupported,"Missing character/material "+name);
                require(asset.poses.Length==7,"Gun and knife pose count "+name);
                foreach(var pose in asset.poses)
                    foreach(var mesh in pose.frames) require(mesh!=null && mesh.vertexCount>100 && mesh.bounds.size.y<4,"Invalid baked mesh "+name);
                var first=asset.poses[1].frames[0].vertices; var later=asset.poses[1].frames[6].vertices;
                float movement=0; for(int i=0;i<first.Length;i++) movement+=(first[i]-later[i]).sqrMagnitude;
                require(movement>.01f,"Run animation did not deform "+name);
                require(asset.poses[3].frames[11].bounds.size.y<asset.poses[0].frames[0].bounds.size.y*.8f,"Death does not lower body "+name);
                if(name=="Zombie")basic=asset; if(name=="Exploder")explosive=asset;
            }
            require(basic.poses[0].frames[0].vertexCount!=explosive.poses[0].frames[0].vertexCount,"Exploder accidentally uses ordinary model");
            var human=Resources.Load<CharacterFrames>("CharacterGenerated/Human");
            require(human.poses[4].source_clip=="Punch"&&human.poses[4].frames[0].vertexCount!=human.poses[2].frames[0].vertexCount,"Knife attack has separate animation and weapon mesh");
            Debug.Log("[CharacterVisualSmoke] PASS model/material imports, 7 gun/knife poses, run deformation, death fall, distinct exploder and knife mesh");
        }
        private static void require(bool condition,string message) { if(!condition)throw new InvalidOperationException("Character visuals: "+message); }
    }
}
