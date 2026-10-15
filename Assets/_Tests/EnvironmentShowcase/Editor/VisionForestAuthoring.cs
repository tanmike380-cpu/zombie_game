using System;
using UnityEditor;
using UnityEngine;
using ZombieGame.Tests.EnvironmentShowcase;

namespace ZombieGame.Tests.EditorTools
{
    /// <summary>Create the initial forest composition once; later Inspector edits are retained.</summary>
    public static class VisionForestAuthoring
    {
        private const string CONFIG_PATH = "Assets/_Tests/EnvironmentShowcase/Settings/VisionConnectedForestV2.asset";

        public static VisionForestConfig prepare_config()
        {
            var existing = AssetDatabase.LoadAssetAtPath<VisionForestConfig>(CONFIG_PATH);
            if(existing)
            {
                existing.validate();
                return existing;
            }
            var config = ScriptableObject.CreateInstance<VisionForestConfig>();
            config.version = "vision-connected-forest-v2";
            config.forest_masses = new[]
            {
                region(.48f,.20f,24,32),region(.75f,.18f,34,26),region(.91f,.50f,22,42),
                region(.75f,.90f,40,25),region(.43f,.80f,22,29),region(.42f,.51f,18,28),
                region(.60f,.70f,26,24),region(.75f,.39f,20,17)
            };
            config.clearings = new[]
            {
                // Each actual source also retains its measured footprint/crown clearance.
                // No giant settlement ellipse: forest tongues may enter the spaces between local openings.
                region(.475f,.705f,3,2.4f),region(.845f,.42f,3,3),region(.415f,.415f,2.5f,3)
            };
            var hub = new Vector2(.40f,.79f);
            config.corridors = new[]
            {
                corridor("North clearing",4,hub,new Vector2(.48f,.83f),new Vector2(.58f,.90f)),
                corridor("Coastal clearing",4.5f,hub,new Vector2(.38f,.69f),new Vector2(.35f,.60f)),
                corridor("East pool-side clearing",4,hub,new Vector2(.61f,.86f),new Vector2(.86f,.86f),new Vector2(.94f,.69f),new Vector2(.94f,.55f)),
                corridor("South settlement opening",4.5f,hub,new Vector2(.365f,.67f),new Vector2(.35f,.49f),
                    new Vector2(.35f,.32f),new Vector2(.56f,.28f),new Vector2(.64f,.24f))
            };
            config.review_focus = new[]{new Vector2(.58f,.72f),new Vector2(.60f,.67f),new Vector2(.80f,.22f),new Vector2(.91f,.48f)};
            config.validate();
            AssetDatabase.CreateAsset(config,CONFIG_PATH);
            return config;
        }

        public static void require_approved_oak(DarkWildernessConfig preset)
        {
            if(preset.tree_prefabs.Length != 1 || !preset.tree_prefabs[0] || preset.tree_prefabs[0].name != "OakLauris71")
                throw new InvalidOperationException("Connected Vision forest requires the already imported original OakLauris71 assembly.");
            var group = preset.tree_prefabs[0].GetComponent<LODGroup>();
            if(!group || group.GetLODs().Length != 2)
                throw new InvalidOperationException("Connected Vision forest requires the original two authored Oak LODs.");
        }

        private static VisionForestConfig.Clearing region(float x,float z,float width,float depth)
        {
            return new VisionForestConfig.Clearing{center=new Vector2(x,z),radius=new Vector2(width,depth)};
        }

        private static VisionForestConfig.Corridor corridor(string name,float half_width,params Vector2[] points)
        {
            return new VisionForestConfig.Corridor{name=name,half_width=half_width,points=points};
        }
    }
}
