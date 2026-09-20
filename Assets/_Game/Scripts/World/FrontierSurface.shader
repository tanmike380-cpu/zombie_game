Shader "ZombieGame/FrontierSurface"
{
    Properties { _Color ("Earth palette", Color) = (0.4,0.35,0.22,1) _Glossiness ("Roughness response", Range(0,1)) = 0.12 }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows
        #pragma target 3.0
        fixed4 _Color; half _Glossiness;
        struct Input { float3 worldPos; float3 worldNormal; };
        float hash(float2 p) { return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453); }
        float noise(float2 p)
        {
            float2 i=floor(p),f=frac(p);f=f*f*(3-2*f);
            return lerp(lerp(hash(i),hash(i+float2(1,0)),f.x),lerp(hash(i+float2(0,1)),hash(i+1),f.x),f.y);
        }
        void surf(Input i,inout SurfaceOutputStandard o)
        {
            float2 p=abs(i.worldNormal.y)>.6?i.worldPos.xz:i.worldPos.xy+i.worldPos.zy;
            float broad=noise(p*.32),detail=noise(p*9),grain=noise(p*43);
            float variation=.76+broad*.32+detail*.12+grain*.07;
            float ground=1-smoothstep(.10,.20,i.worldPos.y);
            float patches=noise(p*.15)*noise(p*1.9);
            float3 dirt=lerp(_Color.rgb,float3(.27,.215,.125),ground*smoothstep(.28,.57,patches)*.55);
            o.Albedo=dirt*variation;
            o.Metallic=0;o.Smoothness=_Glossiness;o.Occlusion=.85+.15*detail;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
