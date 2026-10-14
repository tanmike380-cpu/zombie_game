using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using ZombieGame.Tests.EnvironmentShowcase;

namespace ZombieGame.Tests.EditorTools
{
    /// <summary>Editor-only rendering adapter for the same source-mesh batches; no duplicated plant GameObjects or gameplay controller.</summary>
    [InitializeOnLoad]
    public static class VisionEditorFoliage
    {
        private sealed class Cache
        {
            public EnvironmentShowcaseConfig config;
            public ShowcaseFoliage foliage;
        }
        private static readonly Dictionary<VisionEditorReference,Cache> caches = new Dictionary<VisionEditorReference,Cache>();
        private static readonly HashSet<VisionEditorReference> failed = new HashSet<VisionEditorReference>();

        static VisionEditorFoliage()
        {
            Camera.onPreCull += draw_reference;
            AssemblyReloadEvents.beforeAssemblyReload += clear_cache;
            EditorApplication.playModeStateChanged += state=>clear_cache();
            EditorApplication.hierarchyChanged += clear_cache;
        }

        private static void draw_reference(Camera camera)
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode) return;
            foreach(var marker in UnityEngine.Object.FindObjectsByType<VisionEditorReference>(FindObjectsSortMode.None))
            {
                if(!marker.config || !marker.terrain || !marker.gameObject.activeInHierarchy) continue;
                if(camera.cameraType!=CameraType.SceneView && camera!=marker.comparison_camera) continue;
                if(failed.Contains(marker)) continue;
                try
                {
                    if(!caches.TryGetValue(marker,out var cache))
                    {
                        cache = build_cache(marker);
                        caches.Add(marker,cache);
                    }
                    cache.foliage.draw_batches(camera);
                }
                catch(Exception exception)
                {
                    failed.Add(marker);
                    Debug.LogError("Vision Editor source preview failed; no placeholder foliage generated: "+exception.Message);
                }
            }
        }

        private static Cache build_cache(VisionEditorReference marker)
        {
            var config = UnityEngine.Object.Instantiate(marker.config);
            config.hideFlags = HideFlags.HideAndDontSave;
            VisionWildernessTools.select_vision(config);
            var ground = new ShowcaseTerrain(config,marker.terrain,config.biomes[0]);
            return new Cache{config=config,foliage=new ShowcaseFoliage(config,ground)};
        }

        private static void clear_cache()
        {
            foreach(var cache in caches.Values)
            {
                cache.foliage.Dispose();
                if(cache.config && !EditorUtility.IsPersistent(cache.config))
                    UnityEngine.Object.DestroyImmediate(cache.config);
            }
            caches.Clear();
            failed.Clear();
        }
    }
}
