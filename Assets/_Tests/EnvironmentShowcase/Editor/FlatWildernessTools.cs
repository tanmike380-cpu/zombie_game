using System;
using UnityEditor;
using UnityEngine;
using ZombieGame.Tests.EnvironmentShowcase;

namespace ZombieGame.Tests.EditorTools
{
    /// <summary>Independent flat presentation preset, referencing the already-verified frozen sources unchanged.</summary>
    public static class FlatWildernessTools
    {
        private const string ROOT = "Assets/_Tests/EnvironmentShowcase/Settings/";

        public static void prepare_and_build_flat_preview()
        {
            EnvironmentShowcaseTools.create_scene();
            var config = AssetDatabase.LoadAssetAtPath<EnvironmentShowcaseConfig>(EnvironmentShowcaseTools.CONFIG_PATH);
            if(!config.dark_layout || !config.dark_layout.layout)
                throw new InvalidOperationException("Prepare the frozen dark sources before the independent flat preview.");
            var flat = prepare_flat_preset(config.dark_layout);
            config.flat_layout = flat;
            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();
            EnvironmentShowcaseTools.build_player();
        }

        private static DarkWildernessConfig prepare_flat_preset(DarkWildernessConfig frozen)
        {
            string path = ROOT+"FlatWildernessConfig.asset";
            var flat = AssetDatabase.LoadAssetAtPath<DarkWildernessConfig>(path);
            if(flat)
            {
                flat.ambient_mode = UnityEngine.Rendering.AmbientMode.Trilight;
                EditorUtility.SetDirty(flat);
                return flat;
            }
            flat = UnityEngine.Object.Instantiate(frozen);
            flat.name = "FlatWildernessConfig";
            flat.version = "flat-v1";
            flat.source_record = ROOT+"flat-handoff.json";
            flat.flat_ground = true;
            flat.flat_height = 4.2f;
            flat.ambient_mode = UnityEngine.Rendering.AmbientMode.Trilight;
            flat.layout = prepare_flat_geometry(frozen.layout,flat.source_record);
            flat.grass_tint = new Color(.82f,.94f,.78f);
            flat.dry_tint = new Color(.86f,.83f,.70f);
            flat.soil_tint = new Color(.80f,.74f,.64f);
            flat.sun_intensity = .95f;
            flat.exposed_centers = new[]{new Vector2(.61f,.49f),new Vector2(.48f,.71f),new Vector2(.84f,.42f)};
            flat.exposed_radii = new[]{new Vector2(19,17),new Vector2(8,7),new Vector2(8,6)};
            flat.vegetation_centers = new[]{new Vector2(.43f,.59f),new Vector2(.59f,.72f),new Vector2(.88f,.58f),new Vector2(.47f,.27f)};
            flat.vegetation_radii = new[]{new Vector2(11,9),new Vector2(14,8),new Vector2(9,12),new Vector2(14,8)};
            arrange_source_clusters(flat);
            AssetDatabase.CreateAsset(flat,path);
            EditorUtility.SetDirty(flat);
            return flat;
        }

        private static ConceptLayoutConfig prepare_flat_geometry(ConceptLayoutConfig previous,string record)
        {
            var layout = UnityEngine.Object.Instantiate(previous);
            layout.name = "FlatConceptLayout";
            layout.handoff_record = record;
            layout.meadow_height = 0;
            layout.coast_height = 4.2f;
            layout.pond_center = new Vector2(.80f,.80f);
            layout.pond_radius = new Vector2(13,9);
            layout.pond_level = 3.6f;
            layout.pond_bank_width = 3.5f;
            layout.camera_pitch = 42;
            layout.camera_yaw = -12;
            layout.camera_sizes = new[]{11f,23f,43f};
            layout.views = new[]{new Vector2(.61f,.50f),new Vector2(.32f,.53f),layout.pond_center,new Vector2(.56f,.55f)};
            layout.sea_deep = new Color(.075f,.18f,.25f);
            layout.sea_shallow = new Color(.21f,.32f,.34f);
            layout.pond_deep = new Color(.09f,.18f,.18f);
            layout.pond_shallow = new Color(.25f,.34f,.28f);
            layout.missing_art = "Dense crown trees, reeds and long authored cliff modules unavailable. Native shrubs/grass/rocks are not substitutes. No roads, rivers or gameplay.";
            AssetDatabase.CreateAsset(layout,ROOT+"FlatConceptLayout.asset");
            return layout;
        }

        private static void arrange_source_clusters(DarkWildernessConfig flat)
        {
            var centers = new[]{
                new Vector2(.58f,.43f),new Vector2(.70f,.48f),new Vector2(.57f,.58f),
                new Vector2(.70f,.60f),new Vector2(.46f,.50f),new Vector2(.78f,.54f),new Vector2(.73f,.36f),
                new Vector2(.82f,.40f),new Vector2(.86f,.41f),new Vector2(.85f,.44f),
                new Vector2(.45f,.70f),new Vector2(.48f,.72f),new Vector2(.50f,.69f),
                new Vector2(.40f,.39f),new Vector2(.44f,.405f),new Vector2(.39f,.44f)
            };
            if(flat.placements.Length != centers.Length)
                throw new InvalidOperationException("Expected the exact accepted seven buildings and nine mineral/stone fixtures.");
            for(int index=0;index<centers.Length;index++)
                flat.placements[index].center = centers[index];
        }
    }
}
