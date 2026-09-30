using System;
using UnityEngine;
using ZombieGame.Presentation;

namespace ZombieGame.CharacterTests
{
    public static class CharacterVisualChecks
    {
        public static void run_explosion_feedback()
        {
            using (var feedback = new ExplosionFeedback(Shader.Find("Standard")))
            {
                var flashes = new ZombieGame.Combat.BattleSimulation.Flash[256];
                float expires = Time.time + .5f;
                flashes[0] = new ZombieGame.Combat.BattleSimulation.Flash { origin = Vector3.zero, expires = expires };
                feedback.draw_events(flashes, null, true);
                require(feedback.visible_events == 1, "Active explosion visual missing");
                feedback.draw_events(flashes, null, false);
                require(feedback.visible_events == 0, "Explosion visual reveals fog");
                require(flashes[0].expires == expires && flashes[0].origin == Vector3.zero, "Explosion renderer mutated source events");
                flashes[0] = default;
                feedback.draw_events(flashes, null, true);
                require(feedback.visible_events == 0, "Expired explosion visual leaked");
            }
            Debug.Log("[ExplosionFeedbackChecks] PASS active/expired/hidden events and immutable presentation input; renderer has no damage/noise dispatch");
        }

        public static void run_tripo_original()
        {
            var asset = Resources.Load<CharacterFrames>("TripoArcher/OriginalRest");
            require(asset != null && asset.material.mainTexture != null, "Original Tripo material missing");
            require(asset.poses[0].frames[0].triangles.Length / 3 == 9923, "Original Tripo topology changed");
            var fixture = new GameObject("Group reset null-simulation fixture");
            try { fixture.AddComponent<ZombieGame.Controls.RtsBattleInput>().clear_groups(); }
            finally { UnityEngine.Object.DestroyImmediate(fixture); }
            verify_crowd_culling(asset);
            Debug.Log("[TripoOriginalChecks] PASS original mesh and null-simulation group reset");
        }

        private static void verify_crowd_culling(CharacterFrames asset)
        {
            var root = new GameObject("Crowd culling regression camera");
            try
            {
                var view = root.AddComponent<Camera>();
                view.enabled = false; view.orthographic = true; view.orthographicSize = 2;
                view.aspect = 1; view.nearClipPlane = .1f; view.farClipPlane = 20;
                view.transform.position = new Vector3(0, 1, -5);
                var renderer = new CharacterCrowdRenderer(4, null, asset);
                renderer.begin_frame(view);
                foreach (var position in new[] { Vector3.zero, new Vector3(2.2f, 0, 0), new Vector3(100, 0, 0), new Vector3(0, 0, -50) })
                    renderer.add(true, CharacterPose.Idle, 0, position, Quaternion.identity, human_id: "archer");
                require(renderer.submitted == 2 && renderer.culled == 2, "Offscreen / viewport-edge culling regression");
                renderer.begin_frame();
                renderer.add(true, CharacterPose.Idle, 0, new Vector3(100, 0, 0), Quaternion.identity, human_id: "archer");
                require(renderer.submitted == 1 && renderer.culled == 0, "Default production renderer changed without opting in");
                Debug.Log("[CrowdCullingChecks] PASS visible, partial-edge, offscreen, behind-camera and disabled-culling cases");
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        public static void run_tripo()
        {
            var layout = new Vector3[2400];
            TripoKitingBattle.fill_layout(layout, 400);
            var occupied = new System.Collections.Generic.HashSet<Vector3>(layout);
            require(occupied.Count == 2400, "Kiting fixture spawn overlap");
            require(layout[0].z == 0 && layout[399].z < 0 && layout[400].z == 12 && layout[2399].z < 128,
                "Kiting fixture human/enemy layout or bounds");
            var large_layout = new Vector3[4400];
            TripoKitingBattle.fill_layout(large_layout, 400);
            require(new System.Collections.Generic.HashSet<Vector3>(large_layout).Count == 4400 && large_layout[4399].z < 128,
                "4000 exploder fixture overlaps or exceeds map");
            var exploder_asset = Resources.Load<CharacterFrames>("TripoExploder/Frames");
            require(exploder_asset != null && exploder_asset.poses[0].frames[0].triangles.Length / 3 == 10154,
                "Tripo exploder original topology missing");
            var asset = Resources.Load<CharacterFrames>("TripoCrossbow/Frames");
            require(asset != null && asset.material != null && asset.material.mainTexture != null, "Tripo original texture missing");
            require(asset.poses.Length == 7, "Tripo crowd pose layout");
            require(Mathf.Abs(asset.poses[0].frames[0].bounds.size.y - 2) < .02f, "Tripo unit height");
            foreach (var pose in asset.poses)
            {
                require(pose.frames.Length == 24, "Tripo shared frame count");
                foreach (var mesh in pose.frames)
                    require(mesh != null && mesh.triangles.Length / 3 > 9500 && mesh.bounds.size.y < 3, "Tripo full geometry / scale");
            }
            foreach (int pose in new[] { 1, 2 })
            {
                var first = asset.poses[pose].frames[0].vertices;
                var later = asset.poses[pose].frames[6].vertices;
                float movement = 0;
                for (int i = 0; i < first.Length; i++) movement += (first[i] - later[i]).sqrMagnitude;
                require(movement > .01f, "Tripo imported animation is static: pose=" + pose + " delta=" + movement);
            }
            var muzzle = asset.poses[2].muzzle_positions[0];
            require(muzzle.y > 1 && muzzle.y < 2, "Tripo hand height: " + muzzle);
            Debug.Log("[TripoVisualChecks] PASS original texture, full mesh, scale, idle/run/attack clips and animated vertices");
        }

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
