using System;
using UnityEngine;

namespace ZombieGame.Tests.EnvironmentShowcase
{
    /// <summary>One approved Blender city-wall root, preserving every authored child transform.</summary>
    public sealed class ShowcaseFortifications : IDisposable
    {
        public GameObject instance { get; private set; }
        public VisionFortifiedConfig city { get; private set; }

        public ShowcaseFortifications(EnvironmentShowcaseConfig config,ShowcaseTerrain ground,Transform parent)
        {
            city = config.vision_enabled ? config.active_dark_layout.fortified_city : null;
            if(!city) return;
            city.validate(true);
            var source = city.wall_source;
            instance = UnityEngine.Object.Instantiate(source.prefab,parent);
            instance.name = "Fortified city — intact Blender assembly, open north portal";
            instance.transform.localRotation = source.prefab.transform.localRotation;
            instance.transform.localScale = source.prefab.transform.localScale;
            // The source root is the nominal wall midline origin, not its asymmetric sculpted AABB center.
            instance.transform.position = ground.sample_ground(city.city_center_m.x,city.city_center_m.y)-new Vector3(0,source.native_min_y,0);
            if(instance.GetComponentsInChildren<Collider>(true).Length > 0)
                throw new InvalidOperationException("Fortified art preview must not introduce gameplay collision from the approved visual root.");
        }

        public void Dispose()
        {
            if(instance)
            {
                instance.SetActive(false);
                UnityEngine.Object.Destroy(instance);
            }
        }
    }
}
