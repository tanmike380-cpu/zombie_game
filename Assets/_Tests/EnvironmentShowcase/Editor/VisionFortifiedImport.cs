using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using ZombieGame.Tests.EnvironmentShowcase;

namespace ZombieGame.Tests.EditorTools
{
    /// <summary>Import only a frozen whole Blender wall assembly. Never build or refit wall pieces in Unity.</summary>
    public static class VisionFortifiedImport
    {
        private const string HANDOFF_PATH = "art/fortified_city_handoff.json";
        private const string MASTER_PATH = "art/Blender主文件/ZombieGame.blend";
        private const string ART_ROOT = "Assets/_Tests/EnvironmentShowcase/Art/FortifiedCity/";
        [Serializable] private sealed class Handoff
        {
            public int schema_version;
            public string status,master_sha256;
            public Export export;
            public ComponentRecord[] components;
            public Portal portal;
            public SeamCheck seam_check;
            public InteriorRectangle interior_clear_rectangle_unity_m;
        }
        [Serializable] private sealed class Export
        {
            public string file,sha256;
            public SourceMaterial[] materials;
            public TextureRecord[] textures;
            public Roundtrip roundtrip;
        }
        [Serializable] private sealed class Roundtrip { public bool passed; public int components,uv_loop_samples; public float maximum_vertex_error_m,maximum_uv_error; }
        [Serializable] private sealed class SeamCheck { public bool passed; public int unsealed_sample_rays; public string method,gap_metric_note; }
        [Serializable] private sealed class InteriorRectangle { public float min_x,max_x,min_z,max_z; }
        [Serializable] private sealed class ComponentRecord
        {
            public string name,source_object,source_mesh,source_geometry_uv_sha256;
            public float[] expected_unity_bounds_center_m,expected_unity_bounds_size_m;
            public float local_yaw_unity_degrees;
        }
        [Serializable] private sealed class Portal
        {
            public string status;
            public float[] center_unity_m,outward_direction_unity;
            public float minimum_foot_clearance_m;
        }
        [Serializable] private sealed class SourceMaterial
        {
            public string name,source_material,base_color_texture,base_color_sha256;
            public float[] base_color_multiplier;
            public float metallic,roughness,smoothness;
        }
        [Serializable] private sealed class TextureRecord
        {
            public string source_image,file,sha256,colorspace;
            public int[] size;
        }
        [Serializable] private sealed class SourceRevision
        {
            public string source_blend,master_sha256;
            public string[] source_revision_chain;
            public PreservationProof protected_original_414_proof;
        }
        [Serializable] private sealed class PreservationProof { public bool passed; public ProtectedCounts counts; }
        [Serializable] private sealed class ProtectedCounts { public int objects; }
        [Serializable] private sealed class SourceRegistry { public DiskRevision current_disk_revision; }
        [Serializable] private sealed class DiskRevision { public string master_sha256,handoff; }

        public static void import_frozen_city_walls()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stop the owned review Play session before importing the frozen city source.");
            var handoff = read_frozen_handoff();
            var config = VisionFortifiedTools.prepare_fortified_config();
            Directory.CreateDirectory(ART_ROOT);
            string model_path = copy_verified_city_file(handoff.export.file,ART_ROOT+"FortifiedCityWalls.fbx",handoff.export.sha256);
            AssetDatabase.ImportAsset(model_path,ImportAssetOptions.ForceSynchronousImport);
            var importer = (ModelImporter)AssetImporter.GetAtPath(model_path);
            importer.globalScale = 1;
            importer.useFileScale = true;
            importer.preserveHierarchy = true;
            importer.importAnimation = false;
            importer.importCameras = false;
            importer.importLights = false;
            importer.addCollider = false;
            importer.meshCompression = ModelImporterMeshCompression.Off;
            importer.importNormals = ModelImporterNormals.Import;
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.isReadable = true;
            importer.SaveAndReimport();
            var imported = AssetDatabase.LoadAssetAtPath<GameObject>(model_path);
            if(!imported) throw new InvalidOperationException("Frozen fortified FBX unavailable after import.");
            var instance = UnityEngine.Object.Instantiate(imported);
            try
            {
                var bounds = verify_geometry(instance,handoff.components);
                var material = prepare_original_material(handoff.export);
                foreach(var renderer in instance.GetComponentsInChildren<MeshRenderer>(true))
                {
                    if(renderer.sharedMaterials.Length != 1)
                        throw new InvalidOperationException("Frozen wall piece does not retain its one declared source material: "+renderer.name);
                    renderer.sharedMaterials = new[]{material};
                    renderer.shadowCastingMode = ShadowCastingMode.On;
                    renderer.receiveShadows = true;
                }
                var prefab = PrefabUtility.SaveAsPrefabAsset(instance,ART_ROOT+"FortifiedCityWalls.prefab");
                bind_frozen_assembly(config,handoff,prefab,bounds,material);
                AssetDatabase.SaveAssets();
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(instance);
                importer.isReadable = false;
                importer.SaveAndReimport();
            }
        }

        private static string copy_verified_city_file(string source,string target,string checksum)
        {
            if(!target.StartsWith(ART_ROOT,StringComparison.Ordinal) || target.Contains(".."))
                throw new InvalidOperationException("City copy outside owned Art scope: "+target);
            VisionVegetationImport.verify_hash(source,checksum);
            if(!File.Exists(target)) File.Copy(source,target);
            VisionVegetationImport.verify_hash(target,checksum);
            return target;
        }

        private static Handoff read_frozen_handoff()
        {
            if(!File.Exists(HANDOFF_PATH)) throw new FileNotFoundException("Wait for the Blender specialist's frozen wall handoff.",HANDOFF_PATH);
            var handoff = JsonUtility.FromJson<Handoff>(File.ReadAllText(HANDOFF_PATH));
            if(handoff == null || handoff.schema_version != 1 || handoff.status != "Source frozen; Unity import/visual review pending" || handoff.export == null || handoff.components == null || handoff.components.Length != 19 ||
                handoff.portal == null || handoff.portal.minimum_foot_clearance_m < 6 || handoff.export.materials == null || handoff.export.materials.Length != 1 ||
                handoff.export.textures == null || handoff.export.textures.Length != 1 || handoff.portal.center_unity_m?.Length != 3 ||
                handoff.portal.outward_direction_unity?.Length != 3 || handoff.portal.outward_direction_unity[2] != 1 ||
                handoff.seam_check == null || !handoff.seam_check.passed || handoff.seam_check.unsealed_sample_rays != 0 ||
                handoff.export.roundtrip == null || !handoff.export.roundtrip.passed || handoff.export.roundtrip.components != 19 ||
                handoff.export.roundtrip.maximum_uv_error != 0 || handoff.export.roundtrip.maximum_vertex_error_m > .00002f ||
                handoff.interior_clear_rectangle_unity_m == null || handoff.interior_clear_rectangle_unity_m.max_x <= handoff.interior_clear_rectangle_unity_m.min_x)
                throw new InvalidOperationException("Frozen wall source schema/component/material/open-portal declaration is incomplete.");
            verify_master_revision(handoff.master_sha256);
            VisionVegetationImport.verify_hash(handoff.export.file,handoff.export.sha256);
            if(handoff.export.file != "art/models/fortified-city/FortifiedCityWalls.fbx")
                throw new InvalidOperationException("Wall handoff points outside the designated whole-assembly export.");
            return handoff;
        }

        /// <summary>Accept additive grid authoring only with a registered, re-open-verified preservation chain.</summary>
        private static void verify_master_revision(string frozen_sha256)
        {
            const string GRID_HANDOFF = "art/grid_footprint_handoff.json";
            if(!File.Exists(GRID_HANDOFF))
            {
                VisionVegetationImport.verify_hash(MASTER_PATH,frozen_sha256);
                return;
            }
            var revision = JsonUtility.FromJson<SourceRevision>(File.ReadAllText(GRID_HANDOFF));
            if(revision?.source_revision_chain == null || Array.IndexOf(revision.source_revision_chain,frozen_sha256) < 0)
            {
                VisionVegetationImport.verify_hash(MASTER_PATH,frozen_sha256);
                return;
            }
            var registry = JsonUtility.FromJson<SourceRegistry>(File.ReadAllText("art/blender_master.json"));
            if(revision.source_blend != MASTER_PATH || revision.protected_original_414_proof == null ||
                !revision.protected_original_414_proof.passed || revision.protected_original_414_proof.counts?.objects != 414 ||
                revision.source_revision_chain[revision.source_revision_chain.Length-1] != revision.master_sha256 ||
                registry?.current_disk_revision?.master_sha256 != revision.master_sha256 || registry.current_disk_revision.handoff != GRID_HANDOFF)
                throw new InvalidOperationException("Current Blender revision lacks the registered preservation proof for the frozen city source.");
            VisionVegetationImport.verify_hash(MASTER_PATH,revision.master_sha256);
        }

        private static Bounds verify_geometry(GameObject instance,ComponentRecord[] components)
        {
            var filters = instance.GetComponentsInChildren<MeshFilter>(true);
            if(filters.Length != components.Length) throw new InvalidOperationException("Frozen wall lost a source mesh component.");
            bool first = true;
            var overall = new Bounds();
            foreach(var filter in filters)
            {
                var record = Array.Find(components,item=>item.name == filter.name);
                if(record == null || record.expected_unity_bounds_center_m?.Length != 3 || record.expected_unity_bounds_size_m?.Length != 3 ||
                    string.IsNullOrWhiteSpace(record.source_geometry_uv_sha256) || filter.sharedMesh.subMeshCount != 1 || filter.sharedMesh.uv.Length != filter.sharedMesh.vertexCount)
                    throw new InvalidOperationException("Wall piece provenance/UV/material mapping mismatch: "+filter.name);
                bool first_piece = true;
                var bounds = new Bounds();
                foreach(var vertex in filter.sharedMesh.vertices)
                {
                    var point = filter.transform.TransformPoint(vertex);
                    if(first_piece) { bounds = new Bounds(point,Vector3.zero); first_piece = false; }
                    else bounds.Encapsulate(point);
                    if(first) { overall = new Bounds(point,Vector3.zero); first = false; }
                    else overall.Encapsulate(point);
                }
                for(int axis=0;axis<3;axis++)
                    if(Mathf.Abs(bounds.center[axis]-record.expected_unity_bounds_center_m[axis]) > .003f ||
                        Mathf.Abs(bounds.size[axis]-record.expected_unity_bounds_size_m[axis]) > .003f)
                        throw new InvalidOperationException("Wall native transforms/bounds disagree with Blender handoff: "+filter.name+" actual="+bounds);
            }
            if(first || Mathf.Abs(overall.min.y) > .003f)
                throw new InvalidOperationException("Wall assembly is empty or its shared source floor is not zero.");
            var portal_anchor = find_anchor(instance,"PortalNorth");
            var east_anchor = find_anchor(instance,"EastReference");
            var ground_anchor = find_anchor(instance,"GroundOrigin");
            if(!portal_anchor || !east_anchor || !ground_anchor || portal_anchor.position.z < 20 ||
                east_anchor.position.x < 1 || Mathf.Abs(east_anchor.position.z) > .003f || ground_anchor.position.magnitude > .003f)
                throw new InvalidOperationException("Wall source unit/direction anchors are missing or mirrored. Return to Blender, never refit pieces in Unity.");
            return overall;
        }

        private static Transform find_anchor(GameObject instance,string name)
        {
            foreach(var child in instance.GetComponentsInChildren<Transform>(true)) if(child.name == name) return child;
            return null;
        }

        private static Material prepare_original_material(Export export)
        {
            var source = export.materials[0];
            var texture = export.textures[0];
            if(source.base_color_texture != texture.file || source.base_color_sha256 != texture.sha256 || texture.colorspace != "sRGB" ||
                texture.size == null || texture.size.Length != 2 || source.base_color_multiplier?.Length != 4)
                throw new InvalidOperationException("Wall original packed base-color/material declaration mismatch.");
            VisionVegetationImport.verify_hash(texture.file,texture.sha256);
            // Preserve the FBX's authored relative external-texture directory as well as its bytes.
            Directory.CreateDirectory(ART_ROOT+"FortifiedCityWalls.fbm/");
            string texture_path = copy_verified_city_file(texture.file,ART_ROOT+"FortifiedCityWalls.fbm/"+Path.GetFileName(texture.file),texture.sha256);
            AssetDatabase.ImportAsset(texture_path,ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(texture_path);
            importer.textureType = TextureImporterType.Default;
            importer.sRGBTexture = true;
            importer.maxTextureSize = Mathf.NextPowerOfTwo(Mathf.Max(texture.size[0],texture.size[1]));
            importer.mipmapEnabled = true;
            importer.filterMode = FilterMode.Trilinear;
            importer.anisoLevel = 8;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
            var shader = Shader.Find("ZombieGame/EnvironmentShowcase/SourceOpaque");
            if(!shader) throw new InvalidOperationException("Existing source-opaque adapter unavailable; no replacement shader created.");
            string path = ART_ROOT+"OriginalWall.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if(!material) { material = new Material(shader); AssetDatabase.CreateAsset(material,path); }
            material.shader = shader;
            material.SetTexture("_MainTex",AssetDatabase.LoadAssetAtPath<Texture2D>(texture_path));
            var color = source.base_color_multiplier;
            material.SetColor("_Color",new Color(color[0],color[1],color[2],color[3]).gamma);
            material.SetFloat("_HasRoughness",0);
            material.SetFloat("_RoughnessConstant",source.roughness);
            material.SetFloat("_HasMetallic",0);
            material.SetFloat("_MetallicConstant",source.metallic);
            material.SetFloat("_BumpScale",0);
            material.SetFloat("_Cull",2);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static void bind_frozen_assembly(EnvironmentShowcaseConfig config,Handoff handoff,GameObject prefab,Bounds bounds,Material material)
        {
            var city = config.vision_layout.fortified_city;
            var offset = city.city_center_m;
            var source = new EnvironmentShowcaseConfig.SourceAsset{id="FortifiedCityWalls",prefab=prefab,native_dimensions=bounds.size,native_bounds_center=bounds.center,native_min_y=bounds.min.y,
                source_record=HANDOFF_PATH+"; masterSHA="+handoff.master_sha256+"; FBXSHA="+handoff.export.sha256+"; whole scale 1, original 18x scale already in Blender assembly; no Unity refits.",
                material_bindings=new[]{new EnvironmentShowcaseConfig.SourceMaterialBinding{material=material,base_color=material.GetTexture("_MainTex") as Texture2D,
                    import_channels_pass=true,normal_note="Original packed base colour and source constants only; real normal/roughness/height maps unavailable."}}};
            city.wall_source = source;
            city.source_record = source.source_record;
            city.outer_size_m = new Vector2(bounds.size.x,bounds.size.z);
            city.outer_center_offset_m = new Vector2(bounds.center.x,bounds.center.z);
            city.portal_center_m = offset+new Vector2(handoff.portal.center_unity_m[0],handoff.portal.center_unity_m[2]);
            city.portal_clear_width_m = handoff.portal.minimum_foot_clearance_m;
            city.handoff_source_seams_pass = handoff.seam_check.passed && handoff.seam_check.unsealed_sample_rays == 0;
            city.handoff_max_seam_gap_m = -1;
            city.handoff_seam_note = handoff.seam_check.method+"; "+handoff.seam_check.gap_metric_note;
            var footprints = new List<VisionFortifiedConfig.WallFootprint>();
            foreach(var part in handoff.components)
            {
                var center = new Vector2(part.expected_unity_bounds_center_m[0],part.expected_unity_bounds_center_m[2]);
                var half = new Vector2(part.expected_unity_bounds_size_m[0],part.expected_unity_bounds_size_m[2])*.5f;
                // Bounds are already axis-converted AABBs. Do not rotate them a second time using component yaw.
                footprints.Add(new VisionFortifiedConfig.WallFootprint{id=part.name,center_m=center+offset,half_size_m=half,yaw=0});
                city.handoff_max_footing_gap_m = Mathf.Max(city.handoff_max_footing_gap_m,
                    Mathf.Abs(part.expected_unity_bounds_center_m[1]-part.expected_unity_bounds_size_m[1]*.5f));
            }
            city.wall_footprints = footprints.ToArray();
            var interior = handoff.interior_clear_rectangle_unity_m;
            city.inner_size_m = new Vector2(interior.max_x-interior.min_x,interior.max_z-interior.min_z);
            city.inner_center_offset_m = new Vector2((interior.max_x+interior.min_x)*.5f,(interior.max_z+interior.min_z)*.5f);
            city.detail_focus[1] = city.portal_center_m/config.map_size;
            city.interior_clearance_points_m[4] = city.portal_center_m-new Vector2(0,4);
            city.interior_clearance_points_m[5] = city.portal_center_m;
            city.interior_clearance_points_m[6] = city.portal_center_m+new Vector2(0,4);
            var forest = config.vision_layout.vision_forest;
            var approach = Array.Find(forest.corridors,item=>item.name == "Open north city approach");
            if(approach == null) throw new InvalidOperationException("Fortified preview lost its explicit city approach corridor.");
            approach.points[1] = new Vector2(city.portal_center_m.x,71)/config.map_size;
            approach.points[2] = (city.portal_center_m-new Vector2(0,2))/config.map_size;
            forest.review_focus[1] = city.portal_center_m/config.map_size;
            city.validate(true);
            EditorUtility.SetDirty(city);
            EditorUtility.SetDirty(forest);
        }
    }
}
