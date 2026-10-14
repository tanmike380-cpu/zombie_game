Shader "ZombieGame/EnvironmentShowcase/VegetationPbr"
{
    Properties
    {
        _MainTex ("Source base color + alpha", 2D) = "white" {}
        _BumpMap ("Source normal GL", 2D) = "bump" {}
        _MetallicGlossMap ("Metallic zero + smoothness alpha", 2D) = "black" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _Cutoff ("Source alpha cutoff", Range(0,1)) = .5
        _BumpScale ("Normal strength", Range(0,2)) = 1
        _SpecularStrength ("Bounded dielectric reflection", Range(0,.04)) = .01
        _SmoothnessCap ("Smoothness ceiling", Range(0,1)) = .08
        _NormalFadeStart ("Normal fade start metres", Float) = 20
        _NormalFadeEnd ("Normal fade end metres", Float) = 65
        _Cull ("Cull", Float) = 0
        _AlphaToMask ("Preview alpha coverage", Float) = 0
        _MipBias ("Preview colour mip bias", Float) = 0
        _UseConstantSmoothness ("Source has constant roughness only", Float) = 0
        _ConstantSmoothness ("Declared constant smoothness", Range(0,1)) = .2
        _HasNormal ("Actual source normal map present", Float) = 1
    }
    SubShader
    {
        Tags { "Queue"="AlphaTest" "RenderType"="TransparentCutout" }
        LOD 200
        Cull [_Cull]
        AlphaToMask [_AlphaToMask]
        CGPROGRAM
        #pragma surface surf StandardSpecular fullforwardshadows alphatest:_Cutoff addshadow
        #pragma target 3.0
        #pragma multi_compile_instancing
        #include "UnityStandardUtils.cginc"
        sampler2D _MainTex, _BumpMap, _MetallicGlossMap;
        fixed4 _Color;
        half _BumpScale, _SpecularStrength, _SmoothnessCap;
        float _NormalFadeStart, _NormalFadeEnd, _MipBias;
        float _UseConstantSmoothness, _ConstantSmoothness, _HasNormal;
        struct Input { float2 uv_MainTex; float3 worldPos; float facing : VFACE; };
        void surf(Input input, inout SurfaceOutputStandardSpecular output)
        {
            fixed4 albedo = tex2Dbias(_MainTex, float4(input.uv_MainTex,0,_MipBias)) * _Color;
            output.Albedo = albedo.rgb;
            output.Alpha = albedo.a;
            output.Specular = _SpecularStrength;
            output.Smoothness = min(tex2D(_MetallicGlossMap, input.uv_MainTex).a, _SmoothnessCap);
            if(_UseConstantSmoothness>.5) output.Smoothness=min(_ConstantSmoothness,_SmoothnessCap);
            float planar_distance = length(input.worldPos.xz - _WorldSpaceCameraPos.xz);
            half detail_weight = 1 - smoothstep(_NormalFadeStart, _NormalFadeEnd, planar_distance);
            output.Normal = UnpackScaleNormal(tex2D(_BumpMap, input.uv_MainTex), _BumpScale * detail_weight) * (input.facing >= 0 ? 1 : -1);
            if(_HasNormal<.5) output.Normal=half3(0,0,input.facing>=0?1:-1);
            output.Occlusion = 1;
        }
        ENDCG
    }
    FallBack "Transparent/Cutout/Diffuse"
}
