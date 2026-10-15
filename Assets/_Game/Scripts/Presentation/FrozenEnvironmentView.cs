using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace ZombieGame.Presentation
{
    /// <summary>Plays frozen mesh-instance batches exported by an Editor bridge, without a dependency on review tools.</summary>
    public sealed class FrozenEnvironmentView : MonoBehaviour
    {
        [Serializable] public sealed class Batch
        {
            public string source_id;
            public Mesh mesh;
            public Material material;
            public int submesh;
            public Matrix4x4[] matrices;
            public Bounds bounds;
            public float draw_distance,lod_height,lod_min_height,lod_max_height;
            public bool grass,tree;
        }
        public Batch[] batches = Array.Empty<Batch>();
        public float tree_shadow_distance = 100;
        public int frozen_tree_count,frozen_grass_count;
        private readonly Plane[] frustum = new Plane[6];
        private Matrix4x4[][] submitted;
        private Bounds[] world_bounds;
        private Matrix4x4 cached_transform;
        private bool has_shadow_snapshot;
        private float previous_shadow_distance;
        private int previous_shadow_cascades;
        private ShadowResolution previous_shadow_resolution;

        private void OnEnable(){Camera.onPreCull += draw_environment;submitted = null;}
        private void OnDisable()
        {
            Camera.onPreCull -= draw_environment;
            if(!has_shadow_snapshot)return;
            QualitySettings.shadowDistance = previous_shadow_distance;
            QualitySettings.shadowCascades = previous_shadow_cascades;
            QualitySettings.shadowResolution = previous_shadow_resolution;
            has_shadow_snapshot = false;
        }

        /// <summary>Apply scene-local presentation settings and restore the user's project values on teardown.</summary>
        public void apply_shadow_settings(float distance,int cascades,ShadowResolution resolution)
        {
            if(!has_shadow_snapshot)
            {
                previous_shadow_distance = QualitySettings.shadowDistance;
                previous_shadow_cascades = QualitySettings.shadowCascades;
                previous_shadow_resolution = QualitySettings.shadowResolution;
                has_shadow_snapshot = true;
            }
            QualitySettings.shadowDistance = distance;
            QualitySettings.shadowCascades = cascades;
            QualitySettings.shadowResolution = resolution;
        }

        /// <summary>Scenario-only crop policy. Authored geometry, materials and every non-crop renderer remain unchanged.</summary>
        public int set_crop_shadows(bool cast_shadows)
        {
            int changed = 0;
            foreach(var renderer in GetComponentsInChildren<Renderer>(true))
            {
                string name = renderer.name;
                if(!name.StartsWith("Pasture",StringComparison.Ordinal) ||
                    !(name.EndsWith("_crop",StringComparison.Ordinal) || name.EndsWith("_crop_completion",StringComparison.Ordinal)))continue;
                renderer.shadowCastingMode = cast_shadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
                renderer.receiveShadows = true;changed++;
            }
            return changed;
        }

        private void rebuild_world_batches()
        {
            cached_transform = transform.localToWorldMatrix;
            submitted = new Matrix4x4[batches.Length][];
            world_bounds = new Bounds[batches.Length];
            for(int index = 0; index < batches.Length; index++)
            {
                var batch = batches[index];
                submitted[index] = new Matrix4x4[batch.matrices.Length];
                for(int instance = 0; instance < batch.matrices.Length; instance++)
                    submitted[index][instance] = cached_transform*batch.matrices[instance];
                // The snapshot root is only translated, never scaled or rotated by the scenario.
                world_bounds[index] = new Bounds(cached_transform.MultiplyPoint3x4(batch.bounds.center),batch.bounds.size);
            }
        }

        private void draw_environment(Camera camera)
        {
            if(!camera || camera != Camera.main || !SystemInfo.supportsInstancing) return;
            if(submitted == null || cached_transform != transform.localToWorldMatrix) rebuild_world_batches();
            GeometryUtility.CalculateFrustumPlanes(camera,frustum);
            var forward = camera.transform.forward;
            float ground_height = transform.position.y+4.2f;
            float distance = Mathf.Abs(forward.y) > .001f ? (ground_height-camera.transform.position.y)/forward.y : 0;
            var focus = camera.transform.position+forward*distance;
            for(int index = 0; index < batches.Length; index++)
            {
                var batch = batches[index];
                if(!batch.mesh || !batch.material || batch.matrices.Length == 0) continue;
                float relative_height = batch.lod_height/(camera.orthographicSize*2);
                if(batch.lod_height > 0 && (relative_height < batch.lod_min_height || relative_height >= batch.lod_max_height)) continue;
                var delta = world_bounds[index].center-focus;delta.y = 0;
                if(delta.sqrMagnitude > batch.draw_distance*batch.draw_distance || !GeometryUtility.TestPlanesAABB(frustum,world_bounds[index])) continue;
                bool shadows = !batch.grass && (!batch.tree || delta.sqrMagnitude <= tree_shadow_distance*tree_shadow_distance);
                Graphics.DrawMeshInstanced(batch.mesh,batch.submesh,batch.material,submitted[index],submitted[index].Length,
                    null,shadows ? ShadowCastingMode.On : ShadowCastingMode.Off,true,0,camera,LightProbeUsage.Off);
            }
        }
    }
}
