using System;
using UnityEngine;

namespace ZombieGame.Presentation
{
    [Serializable]
    public sealed class VisualStyle
    {
        public string id, name, description, grass, stone, timber, roof, plaster, cloth, metal, accent, skin, sky, sunlight, ink;
        public float head_scale, shoulder_scale, sun_intensity, camera_pitch, camera_yaw, roughness;
        public Color color(string value)
        {
            if(!ColorUtility.TryParseHtmlString(value,out var result))throw new InvalidOperationException("Invalid art palette: "+id+" "+value);
            return result;
        }
    }
    [Serializable] public sealed class VisualStyleCatalog { public VisualStyle[] styles; }

    /// <summary>Presentation only. Never changes population, collisions, vision or combat balance.</summary>
    public static class VisualStyles
    {
        private static VisualStyleCatalog catalog;
        public static int index {get;private set;}=2;
        public static VisualStyle current => all[index];
        public static VisualStyle[] all
        {
            get
            {
                if(catalog==null)
                {
                    var source=Resources.Load<TextAsset>("VisualStylePresets");
                    if(source==null)throw new InvalidOperationException("Missing VisualStylePresets.json");
                    catalog=JsonUtility.FromJson<VisualStyleCatalog>(source.text);
                    if(catalog==null||catalog.styles==null||catalog.styles.Length!=3)throw new InvalidOperationException("Expected three visual styles");
                    string[] ids={"fortress","dusk","dynasty"};
                    for(int i=0;i<ids.Length;i++)validate(catalog.styles[i],ids[i]);
                }
                return catalog.styles;
            }
        }
        public static void select(int value)
        {if(value<0||value>=all.Length)throw new ArgumentOutOfRangeException(nameof(value));index=value;}
        private static void validate(VisualStyle style,string expected_id)
        {
            if(style==null||style.id!=expected_id||string.IsNullOrWhiteSpace(style.name))throw new InvalidOperationException("Invalid visual style identity: "+expected_id);
            foreach(string color in new[]{style.grass,style.stone,style.timber,style.roof,style.plaster,style.cloth,style.metal,style.accent,style.skin,style.sky,style.sunlight,style.ink})style.color(color);
            if(!(style.head_scale>.3f&&style.head_scale<1.5f&&style.shoulder_scale>.5f&&style.shoulder_scale<2&&
                 style.sun_intensity>0&&style.sun_intensity<3&&style.camera_pitch>20&&style.camera_pitch<75&&
                 style.camera_yaw>=0&&style.camera_yaw<360&&style.roughness>=0&&style.roughness<=1))
                throw new InvalidOperationException("Visual style dimensions/lighting out of range: "+expected_id);
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void reset(){catalog=null;index=2;}
    }
}
