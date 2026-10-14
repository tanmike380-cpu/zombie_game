Shader "ZombieGame/EnvironmentShowcase/ConceptWater"
{
    Properties
    {
        _HeightField("Actual normalized Terrain heights",2D)="black"{}
        _Deep("Deep water",Color)=(.035,.17,.28,1)
        _Shallow("Shallow water",Color)=(.15,.43,.46,1)
        _TerrainOrigin("Terrain origin",Vector)=(0,0,0,0)
        _TerrainSize("Terrain metres",Float)=128
        _HeightRange("Height metres",Float)=18
        _WaterLevel("Independent surface metres",Float)=1.1
        _IsPond("Finite closed pool",Float)=0
        _PoolCenter("Pool world center",Vector)=(0,0,0,0)
        _PoolRadius("Pool radii metres",Vector)=(15,11,0,0)
        _CoastX("Local coastline metres",Float)=32
        _NaturalPreview("Independent irregular dark preview",Float)=0
        _VisionWater("Vision-only surface variation",Float)=0
        _VisionVariation("Vision low-contrast water variation",Float)=.1
        _VisionDepthRange("Vision depth colour range metres",Float)=1.8
        _VisionResponse("Vision bounded water response revision",Float)=0
        _VisionBed("Existing Mud shallow-bed approximation",2D)="gray"{}
        _VisionBedTile("Mud world tile size and offset",Vector)=(4,4,0,0)
        _VisionBedVisibility("Shallow-bed visibility",Range(0,1))=.48
        _VisionBedDepth("Bed attenuation metres",Float)=.75
        _VisionWaveStrength("Small surface slopes",Range(0,1))=.16
        _VisionSmoothness("Water smoothness",Range(0,1))=.78
        _VisionSkyResponse("Approximate Fresnel sky response",Range(0,1))=.18
        _VisionSkyColor("Local sky approximation, not a reflection map",Color)=(.30,.34,.38,1)
    }
    SubShader
    {
        Tags {"RenderType"="Opaque" "Queue"="Geometry+10"}
        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows
        #pragma target 3.0
        sampler2D _HeightField;
        float4 _TerrainOrigin,_PoolCenter,_PoolRadius;
        half4 _Deep,_Shallow;
        float _TerrainSize,_HeightRange,_WaterLevel,_IsPond,_CoastX,_NaturalPreview;
        float _VisionWater,_VisionVariation,_VisionDepthRange;
        sampler2D _VisionBed;
        float4 _VisionBedTile;
        half4 _VisionSkyColor;
        float _VisionResponse,_VisionBedVisibility,_VisionBedDepth;
        float _VisionWaveStrength,_VisionSmoothness,_VisionSkyResponse;
        float water_noise(float2 sample_position)
        {
            float2 cell=floor(sample_position),fraction=frac(sample_position);fraction=fraction*fraction*(3-2*fraction);
            float4 corners=frac(sin(float4(dot(cell,float2(127.1,311.7)),dot(cell+float2(1,0),float2(127.1,311.7)),dot(cell+float2(0,1),float2(127.1,311.7)),dot(cell+1,float2(127.1,311.7))))*43758.5453);
            return lerp(lerp(corners.x,corners.y,fraction.x),lerp(corners.z,corners.w,fraction.x),fraction.y);
        }
        float2 sample_wave_gradient(float2 position)
        {
            const float STEP = .12;
            float wave_x = water_noise(position+float2(STEP,0))-water_noise(position-float2(STEP,0));
            float wave_z = water_noise(position+float2(0,STEP))-water_noise(position-float2(0,STEP));
            return float2(wave_x,wave_z)/(2*STEP);
        }

        struct Input {float3 worldPos; float3 viewDir;};

        void apply_vision_response(Input input,float depth,inout SurfaceOutputStandard output)
        {
            float2 world_position = input.worldPos.xz;
            float2 broad_position = world_position*.72+_Time.y*float2(.055,.021);
            float2 small_position = world_position*2.3+_Time.y*float2(-.032,.041);
            float2 surface_slope = sample_wave_gradient(broad_position)*.72+sample_wave_gradient(small_position)*.28;
            output.Normal = normalize(half3(surface_slope*_VisionWaveStrength,1));
            output.Smoothness = _VisionSmoothness;

            // Project the already licensed Mud source through actual depth attenuation.
            // This is an opaque shallow-bed approximation, not screen-space refraction.
            float2 bed_uv = (world_position-_TerrainOrigin.xz+_VisionBedTile.zw)/max(_VisionBedTile.xy,float2(.1,.1));
            half3 bed_color = tex2D(_VisionBed,bed_uv+surface_slope*.025).rgb;
            float bed_visibility = _VisionBedVisibility*exp(-max(0,depth)/max(.05,_VisionBedDepth));
            float depth_weight = 1-exp(-max(0,depth)/max(.1,_VisionDepthRange));
            half3 water_color = lerp(_Shallow.rgb,_Deep.rgb,depth_weight);
            output.Albedo = lerp(water_color,bed_color*half3(.64,.72,.67),bed_visibility);
            output.Albedo *= 1+(water_noise(world_position*.12)-.5)*_VisionVariation;

            // A bounded local sky-colour response; no reflected scene/SSR/GrabPass.
            float view_cosine = saturate(dot(normalize(input.viewDir),output.Normal));
            float fresnel = .02+.98*pow(1-view_cosine,5);
            output.Emission = _VisionSkyColor.rgb*fresnel*_VisionSkyResponse;
        }
        void surf(Input input,inout SurfaceOutputStandard output)
        {
            float2 uv=(input.worldPos.xz-_TerrainOrigin.xz)/_TerrainSize;
            float depth=_WaterLevel-tex2D(_HeightField,uv).r*_HeightRange;
            if(uv.x<0||uv.y<0||uv.x>1||uv.y>1)
            {
                // Vision continues the clamped native shore depth outside the finite height field.
                // Legacy previews retain their authored outside-sea colour behaviour.
                if(_VisionWater<.5) depth=4;
                if(_IsPond<.5) clip(_CoastX-(input.worldPos.x-_TerrainOrigin.x));
            }
            clip(depth-.012);
            if(_VisionWater>.5 && _VisionResponse>.5)
            {
                output.Metallic=0;
                output.Occlusion=1;
                output.Alpha=1;
                apply_vision_response(input,depth,output);
                return;
            }
            float phase=dot(input.worldPos.xz,float2(3.9,2.57))+sin(dot(input.worldPos.xz,float2(.71,.83)))*1.7-_Time.y*.42;
            float second_phase=dot(input.worldPos.xz,float2(-2.19,1.11))-_Time.y*.23;
            float ripple=sin(phase)*.65+sin(second_phase)*.35;
            if(_NaturalPreview>.5) ripple=(water_noise(input.worldPos.xz*.7+_Time.y*float2(.03,.015))-.5)*.65+(water_noise(input.worldPos.xz*2.1-_Time.y*float2(.012,.03))-.5)*.35;
            output.Albedo=lerp(_Shallow.rgb,_Deep.rgb,saturate(depth/(_IsPond>.5?1.0:.85)))*(1+ripple*.05);
            float shore=(1-smoothstep(.04,.22,depth))*(.5+.5*sin(phase*2));
            if(_NaturalPreview>.5) shore=(1-smoothstep(.02,.10,depth))*(.3+.7*water_noise(input.worldPos.xz*1.4+_Time.y*.01));
            output.Albedo=lerp(output.Albedo,half3(.48,.57,.53),shore*(_IsPond>.5?.08:.3));
            output.Normal=normalize(half3(cos(phase)*.035,cos(second_phase)*.025,1));
            if(_NaturalPreview>.5) output.Normal=normalize(half3(ripple*.06,(water_noise(input.worldPos.xz*.9+_Time.y*.02)-.5)*.05,1));
            output.Smoothness=.4; output.Metallic=0; output.Occlusion=1; output.Alpha=1;
            if(_VisionWater>.5)
            {
                float broad=water_noise(input.worldPos.xz*.12);
                float detail=water_noise(input.worldPos.xz*1.7+_Time.y*float2(.018,.013))-.5;
                float depth_weight=saturate(depth/max(.1,_VisionDepthRange));
                output.Albedo=lerp(_Shallow.rgb,_Deep.rgb,depth_weight)*(1+(broad-.5)*_VisionVariation+detail*.025);
                float edge=(1-smoothstep(.025,.16,depth))*(.25+.75*water_noise(input.worldPos.xz*.9));
                output.Albedo=lerp(output.Albedo,half3(.45,.49,.45),edge*.10);
                float2 wave_position=input.worldPos.xz*.9+_Time.y*float2(.018,.013);
                float wave_x=water_noise(wave_position+float2(.12,0))-water_noise(wave_position-float2(.12,0));
                float wave_z=water_noise(wave_position+float2(0,.12))-water_noise(wave_position-float2(0,.12));
                output.Normal=normalize(half3(wave_x*.25,wave_z*.25,1));
                output.Smoothness=.43;
            }
        }
        ENDCG
    }
    FallBack "Diffuse"
}
