using UnityEngine;
using UnityEngine.Rendering;

namespace ZombieGame.World
{
    /// <summary>One controlled daylight rig; never changes original textures or model proportions.</summary>
    public static class CoastalLighting
    {
        public static void configure(Transform parent)
        {
            QualitySettings.antiAliasing=4;QualitySettings.globalTextureMipmapLimit=0;
            QualitySettings.anisotropicFiltering=AnisotropicFiltering.ForceEnable;
            // Orthographic zoom does not move the camera: the RTS rig stays 180 units away.
            QualitySettings.shadows=ShadowQuality.All;QualitySettings.shadowDistance=260;
            QualitySettings.shadowResolution=ShadowResolution.High;QualitySettings.shadowCascades=4;
            RenderSettings.ambientMode=AmbientMode.Trilight;
            RenderSettings.ambientSkyColor=new Color(.48f,.54f,.64f);
            RenderSettings.ambientEquatorColor=new Color(.32f,.35f,.39f);
            RenderSettings.ambientGroundColor=new Color(.18f,.17f,.14f);
            foreach(var light in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
                if(light.type==LightType.Directional)
                {
                    light.color=new Color(1,.94f,.84f);light.intensity=1.05f;
                    light.transform.rotation=Quaternion.Euler(48,-40,0);light.shadows=LightShadows.Soft;
                    light.shadowStrength=.72f;light.shadowBias=.035f;light.shadowNormalBias=.15f;
                }
            var obj=new GameObject("Settlement reflection probe — one capture");obj.transform.SetParent(parent,false);
            obj.transform.position=new Vector3(-90,5,-89);
            var probe=obj.AddComponent<ReflectionProbe>();probe.mode=ReflectionProbeMode.Realtime;
            probe.refreshMode=ReflectionProbeRefreshMode.OnAwake;probe.timeSlicingMode=ReflectionProbeTimeSlicingMode.IndividualFaces;
            probe.resolution=128;probe.size=new Vector3(90,30,90);probe.boxProjection=true;
            probe.clearFlags=ReflectionProbeClearFlags.SolidColor;probe.backgroundColor=new Color(.38f,.45f,.52f);probe.intensity=.55f;
        }
    }
}
