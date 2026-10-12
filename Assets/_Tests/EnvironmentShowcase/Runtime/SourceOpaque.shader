Shader "ZombieGame/EnvironmentShowcase/SourceOpaque"
{
    Properties
    {
        _MainTex("Authored source base colour",2D)="white"{}
        _BumpMap("Source normal, if available",2D)="bump"{}
        _Roughness("Source roughness, if available",2D)="white"{}
        _MetallicMap("Source metallic, if available",2D)="black"{}
        _HasMetallic("Real metallic map present",Float)=0
        _MetallicConstant("Nonmetal source fallback",Range(0,1))=0
        _MetallicMultiplier("Authored metallic multiplier",Range(0,1))=1
        _Color("Authored linear tint",Color)=(1,1,1,1)
        _HasRoughness("Real roughness map present",Float)=0
        _RoughnessConstant("Authored fallback, not a scanned map",Range(0,1))=.9
        _BumpScale("Source normal strength",Float)=0
        _Cull("Source sides",Float)=0
    }
    SubShader
    {
        Tags {"RenderType"="Opaque"}
        Cull [_Cull]
        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows addshadow
        #pragma target 3.0
        #include "UnityStandardUtils.cginc"
        sampler2D _MainTex,_BumpMap,_Roughness,_MetallicMap;
        fixed4 _Color;
        half _HasRoughness,_RoughnessConstant,_BumpScale;
        half _HasMetallic,_MetallicConstant,_MetallicMultiplier;
        struct Input {float2 uv_MainTex; float facing:VFACE;};
        void surf(Input input,inout SurfaceOutputStandard output)
        {
            output.Albedo=tex2D(_MainTex,input.uv_MainTex).rgb*_Color.rgb;
            output.Normal=UnpackScaleNormal(tex2D(_BumpMap,input.uv_MainTex),_BumpScale)*(input.facing>=0?1:-1);
            output.Metallic=saturate(lerp(_MetallicConstant,tex2D(_MetallicMap,input.uv_MainTex).r*_MetallicMultiplier,_HasMetallic));
            output.Smoothness=1-lerp(_RoughnessConstant,tex2D(_Roughness,input.uv_MainTex).r,_HasRoughness);
            output.Occlusion=1;
            output.Alpha=1;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
