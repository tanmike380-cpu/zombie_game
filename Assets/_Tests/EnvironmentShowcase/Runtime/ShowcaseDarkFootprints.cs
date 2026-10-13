using System;
using System.Collections.Generic;
using UnityEngine;

namespace ZombieGame.Tests.EnvironmentShowcase
{
    /// <summary>Natural settled footing, not connected roads. Dimensions come from each exact source assembly.</summary>
    public sealed class ShowcaseDarkFootprints
    {
        private sealed class Footprint { public Vector2 center,half; public float yaw,height; }
        private readonly List<Footprint> footprints=new List<Footprint>();
        public ShowcaseDarkFootprints(DarkWildernessConfig dark,EnvironmentShowcaseConfig config,Func<float,float,float> base_height)
        {
            if(!dark.props_enabled) return;
            foreach(var placement in dark.placements)
            {
                var source=dark.find_source(config,placement.source_id);
                if(placement.uniform_scale<=0) throw new InvalidOperationException("Invalid whole source scale: "+source.id);
                var center=placement.center*config.map_size;
                var half=new Vector2(source.native_dimensions.x,source.native_dimensions.z)*placement.uniform_scale*.5f+Vector2.one*.65f;
                float extent=half.magnitude+3;
                if(center.x-extent<0||center.y-extent<0||center.x+extent>config.map_size||center.y+extent>config.map_size)
                    throw new InvalidOperationException("Dark source footprint outside map: "+source.id);
                footprints.Add(new Footprint{center=center,half=half,yaw=placement.yaw*Mathf.Deg2Rad,height=base_height(center.x,center.y)});
            }
        }
        private static float measure_box(Vector2 point,Footprint pad)
        {
            var delta=point-pad.center;
            var local=new Vector2(delta.x*Mathf.Cos(pad.yaw)-delta.y*Mathf.Sin(pad.yaw),delta.x*Mathf.Sin(pad.yaw)+delta.y*Mathf.Cos(pad.yaw));
            return new Vector2(Mathf.Max(0,Mathf.Abs(local.x)-pad.half.x),Mathf.Max(0,Mathf.Abs(local.y)-pad.half.y)).magnitude;
        }
        public float sample_height(float x,float z,float original)
        {
            var point = new Vector2(x,z);
            // Resolve every hard source footprint before any neighboring soft transition.
            // Otherwise a later pad's feather can lift/lower an already flattened building.
            foreach (var pad in footprints)
            {
                if (measure_box(point,pad) == 0)
                    return pad.height;
            }
            foreach (var pad in footprints)
            {
                float distance = measure_box(point,pad);
                float weight = 1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.2f,9,distance));
                original = Mathf.Lerp(original,pad.height,weight);
            }
            return original;
        }
        public float soil_weight(float x,float z)
        {
            float weight=0;
            foreach(var pad in footprints) weight=Mathf.Max(weight,1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(0,2.5f,measure_box(new Vector2(x,z),pad))));
            return weight;
        }
        public bool contains(float x,float z) => soil_weight(x,z)>.35f;
        public float softened_footing(float x,float z)
        {
            float weight=0;
            foreach(var pad in footprints) weight=Mathf.Max(weight,1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(1,10,measure_box(new Vector2(x,z),pad))));
            return weight;
        }
    }
}
