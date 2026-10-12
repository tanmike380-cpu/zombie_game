using System;
using UnityEngine;

namespace ZombieGame.Tests.EnvironmentShowcase
{
    /// <summary>Finite interpretation of approved 05/07/09 images, not an extracted height map.</summary>
    public sealed class ConceptLayoutConfig : ScriptableObject
    {
        public string handoff_record = "Assets/_Tests/EnvironmentShowcase/Settings/concept-handoff.json";
        public float coast_x = 32, sea_level = 1.1f, coast_height = 7.8f;
        public Vector2 meadow_center = new Vector2(.54f,.35f), meadow_radius = new Vector2(22,25);
        public float meadow_height = 7;
        public Vector2 pond_center = new Vector2(.75f,.73f), pond_radius = new Vector2(15,11);
        public float pond_level = 3.6f, pond_depth = 1.25f, pond_bank_width = 4;
        public Vector2[] forest_centers = { new Vector2(.60f,.77f),new Vector2(.90f,.48f),new Vector2(.48f,.19f) };
        public int tree_budget = 110;
        public float camera_pitch = 36, camera_yaw = -12;
        public float[] camera_sizes = {11,19,43};
        public Vector3 sun_rotation = new Vector3(35,-55,0);
        public float tree_near_lod_threshold = .065f;
        public TerrainLayer stone_layer;
        public Color sea_deep = new Color(.035f,.17f,.28f), sea_shallow = new Color(.15f,.43f,.46f);
        public Color pond_deep = new Color(.055f,.15f,.17f), pond_shallow = new Color(.22f,.35f,.28f);
        public Vector2[] views = { new Vector2(.56f,.33f),new Vector2(.27f,.49f),new Vector2(.75f,.73f),new Vector2(.55f,.54f) };
        public string missing_art = "Generic approved TreeSmall, not oak/birch/olive/cypress. Reeds/lily pads and giant/overhanging rocks absent; no substitute models. No river, mines, buildings or gameplay.";

        public void validate_config(float size, float height_range)
        {
            if (size != 128 || sea_level <= 0 || pond_level <= sea_level || pond_depth <= 0 || pond_depth >= pond_level ||
                coast_height >= height_range || meadow_height + 4 >= height_range || pond_bank_width < 3 ||
                pond_center.x * size - pond_radius.x - pond_bank_width < coast_x + 15 ||
                pond_center.x * size + pond_radius.x + pond_bank_width + 2 > size ||
                pond_center.y * size + pond_radius.y + pond_bank_width + 2 > size || views.Length != 4)
                throw new InvalidOperationException("Concept geometry/water separation/bounds invalid; adjust interpretation, not clamp.");
        }
    }
}
