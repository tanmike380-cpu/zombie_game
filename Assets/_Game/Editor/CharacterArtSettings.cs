using UnityEditor;
using UnityEngine;

namespace ZombieGame.EditorTools
{
    /// <summary>Authored visual-only settings. Re-bake models after editing; never stores unit balance.</summary>
    public sealed class CharacterArtSettings : ScriptableObject
    {
        private const string PATH = "Assets/_Game/CharacterArtSettings.asset";

        [Header("Palette (re-bake after editing)")]
        [Range(0, 1)] public float saturation = .62f;
        public Color tint = new Color(.91f, .88f, .80f, 1);
        [Range(.1f, .8f)] public float ambient_light = .38f;
        public Color wood = new Color(.23f, .13f, .07f, 1);
        public Color iron = new Color(.17f, .18f, .18f, 1);
        public Color brass = new Color(.37f, .30f, .16f, 1);
        public Color exploder_belly = new Color(.43f, .22f, .28f, .72f);
        public Color exploder_body = new Color(.36f, .30f, .32f, .45f);

        [Header("Bird-gun silhouette (visual dimensions only)")]
        [Range(.03f, .08f)] public float barrel_diameter = .045f;
        [Range(.04f, .12f)] public float stock_width = .065f;

        public static CharacterArtSettings load_settings()
        {
            var settings = AssetDatabase.LoadAssetAtPath<CharacterArtSettings>(PATH);
            if (settings != null) return settings;
            settings = CreateInstance<CharacterArtSettings>();
            AssetDatabase.CreateAsset(settings, PATH);
            AssetDatabase.SaveAssets();
            return settings;
        }

        [MenuItem("Tools/Zombie Game/Characters/Select Art Settings")]
        public static void select_settings()
        {
            Selection.activeObject = load_settings();
            EditorGUIUtility.PingObject(Selection.activeObject);
        }
    }
}
