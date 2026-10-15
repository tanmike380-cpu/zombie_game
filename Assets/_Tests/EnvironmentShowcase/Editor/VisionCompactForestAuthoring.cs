using System;
using UnityEditor;
using UnityEngine;
using ZombieGame.Tests.EnvironmentShowcase;

namespace ZombieGame.Tests.EditorTools
{
    /// <summary>Independent v3 composition: whole-root translations only, retaining the v2 assets for rollback.</summary>
    public static class VisionCompactForestAuthoring
    {
        private const string ROOT = "Assets/_Tests/EnvironmentShowcase/Settings/";
        private const string PRESET_PATH = ROOT+"VisionWildernessV3.asset";
        private const string GEOMETRY_PATH = ROOT+"VisionConceptV3.asset";
        private const string FOREST_PATH = ROOT+"VisionConnectedForestV3.asset";
        private const string SPARSE_PATH = ROOT+"VisionWildernessSparseReview.asset";

        public static DarkWildernessConfig prepare_sparse_review(DarkWildernessConfig original)
        {
            var existing = AssetDatabase.LoadAssetAtPath<DarkWildernessConfig>(SPARSE_PATH);
            if(existing) return existing;
            var preset = UnityEngine.Object.Instantiate(original);
            preset.name = "VisionWildernessSparseReview";
            preset.version = "vision-original-sparse-review";
            preset.vision_forest = null;
            preset.source_record += "; original placement snapshot with original three sparse Oak groups; original preset/v2 forest reference retained.";
            preset.validate_vision_regions();
            AssetDatabase.CreateAsset(preset,SPARSE_PATH);
            return preset;
        }

        public static DarkWildernessConfig prepare_preset(DarkWildernessConfig previous)
        {
            var existing = AssetDatabase.LoadAssetAtPath<DarkWildernessConfig>(PRESET_PATH);
            if(existing)
            {
                existing.validate_vision_regions();
                return existing;
            }
            VisionForestAuthoring.require_approved_oak(previous);
            var preset = UnityEngine.Object.Instantiate(previous);
            preset.name = "VisionWildernessV3";
            preset.version = "vision-connected-v3";
            preset.source_record += "; v3 parent-authorized whole-root translations; original dimensions, yaw, scale, material and authored assembly unchanged. Previous preset retained.";
            preset.placements = copy_compact_placements(previous.placements);
            preset.layout = prepare_geometry(previous.layout);
            preset.vision_forest = prepare_forest();
            preset.validate_vision_regions();
            AssetDatabase.CreateAsset(preset,PRESET_PATH);
            return preset;
        }

        private static DarkWildernessConfig.Placement[] copy_compact_placements(DarkWildernessConfig.Placement[] previous)
        {
            if(previous.Length != 16) throw new InvalidOperationException("Compact Vision requires the approved sixteen-fixture source list.");
            var centers_metres = new[]
            {
                new Vector2(65,31),new Vector2(80,31),new Vector2(65,48),new Vector2(82,48),
                new Vector2(49,45),new Vector2(49,31),new Vector2(80,17),
                new Vector2(113,19),new Vector2(117,20),new Vector2(115,24),
                new Vector2(52,113),new Vector2(56,115),new Vector2(58,111),
                new Vector2(110,114),new Vector2(116,114),new Vector2(113,108)
            };
            var expected_ids = new[]{"Headquarters","Workshop","Pasture","EuropeanStoneHouse","NorthAfricanRuins",
                "MiddleEasternCourtyard","EuropeanStoneHouse","IronOreRock","IronOreRock","IronOreRock",
                "GoldOreRock","GoldOreRock","GoldOreRock","BoulderNative","BoulderNative","BoulderNative"};
            var placements = new DarkWildernessConfig.Placement[previous.Length];
            for(int index=0;index<placements.Length;index++)
            {
                var source = previous[index];
                if(source.source_id != expected_ids[index])
                    throw new InvalidOperationException("Compact Vision source order changed at index "+index+": "+source.source_id);
                placements[index] = new DarkWildernessConfig.Placement
                {
                    source_id=source.source_id,center=centers_metres[index]/128,
                    yaw=source.yaw,uniform_scale=source.uniform_scale,ore=source.ore
                };
            }
            return placements;
        }

        private static ConceptLayoutConfig prepare_geometry(ConceptLayoutConfig previous)
        {
            var existing = AssetDatabase.LoadAssetAtPath<ConceptLayoutConfig>(GEOMETRY_PATH);
            if(existing) return existing;
            var layout = UnityEngine.Object.Instantiate(previous);
            layout.name = "VisionConceptV3";
            layout.views = (Vector2[])previous.views.Clone();
            // Height/water parameter values are copied unchanged. Only the review focus changes.
            layout.views[0] = new Vector2(78,66)/128;
            AssetDatabase.CreateAsset(layout,GEOMETRY_PATH);
            return layout;
        }

        private static VisionForestConfig prepare_forest()
        {
            var existing = AssetDatabase.LoadAssetAtPath<VisionForestConfig>(FOREST_PATH);
            if(existing)
            {
                existing.validate();
                return existing;
            }
            var forest = ScriptableObject.CreateInstance<VisionForestConfig>();
            forest.version = "vision-connected-forest-v3";
            forest.tree_spacing = 6;
            forest.edge_feather = .8f;
            forest.use_native_source_clearance = true;
            forest.forest_masses = new[]
            {
                region(88,110,44,30),region(88,70,47,43),region(88,24,47,32),
                region(55,83,20,44),region(118,70,18,55)
            };
            // Exact source bounds plus one crown radius already reserve each compact local opening.
            forest.clearings = Array.Empty<VisionForestConfig.Clearing>();
            var hub = new Vector2(79,66);
            forest.corridors = new[]
            {
                corridor("North forest opening",4,hub,new Vector2(58,72),new Vector2(45,86),new Vector2(45,108)),
                corridor("Coastal forest opening",4.5f,hub,new Vector2(58,72),new Vector2(47,72)),
                corridor("Pool-side forest opening",4,hub,new Vector2(96,62),new Vector2(118,76)),
                corridor("South settlement opening",4.5f,hub,new Vector2(96,62),new Vector2(96,41),new Vector2(97,21))
            };
            forest.review_focus = new[]{new Vector2(74,89)/128,new Vector2(80,66)/128,new Vector2(106,82)/128,new Vector2(94,42)/128};
            forest.validate();
            AssetDatabase.CreateAsset(forest,FOREST_PATH);
            return forest;
        }

        private static VisionForestConfig.Clearing region(float x,float z,float width,float depth)
        {
            return new VisionForestConfig.Clearing{center=new Vector2(x,z)/128,radius=new Vector2(width,depth)};
        }

        private static VisionForestConfig.Corridor corridor(string name,float half_width,params Vector2[] points_metres)
        {
            var points = new Vector2[points_metres.Length];
            for(int index=0;index<points.Length;index++) points[index] = points_metres[index]/128;
            return new VisionForestConfig.Corridor{name=name,half_width=half_width,points=points};
        }
    }
}
