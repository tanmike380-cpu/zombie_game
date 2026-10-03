using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace ZombieGame.Presentation
{
    /// <summary>Shared sampled meshes, grouped by character/pose/frame. No per-unit skeleton or Animator.</summary>
    public sealed class CharacterCrowdRenderer
    {
        private readonly CharacterFrames[] characters;
        private readonly Dictionary<string,int> imported_indices=new Dictionary<string,int>();
        private readonly Matrix4x4[][] matrices;
        private readonly int[] counts;
        private readonly int frames_per_pose;
        private readonly int pose_count;
        private readonly int capacity;
        private readonly Plane[] view_planes = new Plane[6];
        private bool cull_to_view;
        private readonly bool strict_imports;
        public int submitted { get; private set; }
        public int culled { get; private set; }

        public CharacterCrowdRenderer(int capacity,string style=null,CharacterFrames archer_override=null,string override_unit="archer",CharacterFrames exploder_override=null,ImportedRoster roster=null,bool strict_imports=false)
        {
            this.strict_imports=strict_imports;
            if(capacity<1)throw new ArgumentOutOfRangeException(nameof(capacity));this.capacity=capacity;
            characters = new CharacterFrames[8+(roster==null?0:roster.units.Length)];set_style(style);
            if(roster!=null)for(int i=0;i<roster.units.Length;i++)
            {if(roster.units[i].frames==null)throw new InvalidOperationException("Missing imported animation: "+roster.units[i].unit_id);imported_indices.Add(roster.units[i].unit_id,8+i);characters[8+i]=roster.units[i].frames;}
            if(archer_override!=null)characters[human_index(override_unit)]=archer_override;
            if(exploder_override!=null)characters[2]=exploder_override;
            if (Array.Exists(characters,c=>c==null)) throw new InvalidOperationException("Bake character models before enabling animated crowd");
            pose_count=characters[0].poses.Length;
            int maximum_frames=0;
            foreach(var character in characters)
            {
                pose_count=Math.Max(pose_count,character.poses.Length);
                foreach(var pose in character.poses)
                {
                    if(pose.frames.Length==0)throw new InvalidOperationException("Empty crowd animation");
                    maximum_frames=Math.Max(maximum_frames,pose.frames.Length);
                }
            }
            frames_per_pose=maximum_frames;
            int buckets = characters.Length * pose_count * frames_per_pose;
            matrices = new Matrix4x4[buckets][]; counts = new int[buckets];
            for (int i = 0; i < buckets; i++) matrices[i] = new Matrix4x4[Math.Min(capacity,128)];
        }
        public void set_style(string style)
        {
            string prefix="CharacterGenerated/"+(string.IsNullOrEmpty(style)?"":style+"/");
            string[] names={"Human","Zombie","Exploder","Archer","Repeater","Crossbow","Ballista","Cannon"};
            for(int i=0;i<names.Length;i++)
            {
                var asset=Resources.Load<CharacterFrames>(prefix+names[i]);
                if(asset==null)throw new InvalidOperationException("Missing baked art variant: "+prefix+names[i]);
                characters[i]=asset;
            }
        }

        public float locomotion_time(string unit_id,float travelled,float scale,float fallback)
        {
            if(!imported_indices.TryGetValue(unit_id,out int index))return fallback;
            var art=characters[index];
            return art.locomotion_stride>0?travelled/(art.locomotion_stride*scale)*art.poses[(int)CharacterPose.Run].duration:fallback;
        }

        public void begin_frame(Camera view=null)
        {
            Array.Clear(counts, 0, counts.Length); submitted = culled = 0;
            cull_to_view = view != null;
            if(cull_to_view)GeometryUtility.CalculateFrustumPlanes(view,view_planes);
        }

        public void add(bool human, CharacterPose pose, float age, Vector3 position, Quaternion rotation, bool explosive = false,float model_scale=1,string human_id=null,float attack_window=0)
        {
            if(strict_imports&&(human_id==null||!imported_indices.ContainsKey(human_id)))
                throw new InvalidOperationException("Procedural art fallback forbidden for this scenario: "+human_id);
            int character = human_id!=null&&imported_indices.TryGetValue(human_id,out int imported)?imported:human ? human_index(human_id) : explosive ? 2 : 1;
            if((int)pose>=characters[character].poses.Length) pose=CharacterPose.Run;
            if(character>=8&&attack_window>0&&(pose==CharacterPose.Attack||pose==CharacterPose.MeleeAttack))
                age*=characters[character].poses[(int)pose].duration/attack_window;
            int frame = characters[character].frame_index(pose, age);
            Matrix4x4 matrix = Matrix4x4.TRS(position, rotation, Vector3.one*model_scale);
            if(cull_to_view&&!is_visible(characters[character].poses[(int)pose].frames[frame].bounds,matrix))
            {culled++;return;}
            int bucket = (character * pose_count + (int)pose) * frames_per_pose + frame;
            if(counts[bucket]>=capacity)throw new InvalidOperationException("Crowd frame exceeds declared capacity");
            if(counts[bucket]==matrices[bucket].Length)Array.Resize(ref matrices[bucket],Math.Min(capacity,matrices[bucket].Length*2));
            matrices[bucket][counts[bucket]++] = matrix;
            submitted++;
        }

        private bool is_visible(Bounds local,Matrix4x4 matrix)
        {
            Vector3 x=matrix.MultiplyVector(new Vector3(local.extents.x,0,0));
            Vector3 y=matrix.MultiplyVector(new Vector3(0,local.extents.y,0));
            Vector3 z=matrix.MultiplyVector(new Vector3(0,0,local.extents.z));
            Vector3 extents=new Vector3(Mathf.Abs(x.x)+Mathf.Abs(y.x)+Mathf.Abs(z.x),Mathf.Abs(x.y)+Mathf.Abs(y.y)+Mathf.Abs(z.y),Mathf.Abs(x.z)+Mathf.Abs(y.z)+Mathf.Abs(z.z));
            // Mesh bounds include armor and weapon; do not clip units at their feet on viewport edges.
            return GeometryUtility.TestPlanesAABB(view_planes,new Bounds(matrix.MultiplyPoint3x4(local.center),extents*2));
        }

        public void draw()
        {
            for (int bucket = 0; bucket < counts.Length; bucket++)
            {
                if (counts[bucket] == 0) continue;
                int character = bucket / (pose_count * frames_per_pose), pose = bucket / frames_per_pose % pose_count, frame = bucket % frames_per_pose;
                var parameters = new RenderParams(characters[character].material) { worldBounds = new Bounds(Vector3.zero,new Vector3(260,30,260)), shadowCastingMode = character==0?ShadowCastingMode.On:ShadowCastingMode.Off, receiveShadows = true };
                for (int start = 0; start < counts[bucket]; start += 1023)
                    Graphics.RenderMeshInstanced(parameters, characters[character].poses[pose].frames[frame], 0,
                        matrices[bucket], Math.Min(1023, counts[bucket]-start), start);
            }
        }

        public Vector3 human_muzzle(Vector3 position, Quaternion rotation) => position + rotation * characters[0].poses[2].muzzle_positions[0];
        private static int human_index(string id)
        {
            switch(id){case "archer":return 3;case "repeating_crossbowman":return 4;case "heavy_crossbowman":return 5;case "heavy_ballista":return 6;case "cannon":return 7;default:return 0;}
        }
    }
}
