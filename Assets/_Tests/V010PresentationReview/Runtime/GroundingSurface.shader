Shader "ZombieGame/PresentationReview/GroundingSurface"
{
    Properties
    {
        _MainTex("Authored ground diffuse",2D)="white"{}
        _BumpMap("Authored ground GL normal",2D)="bump"{}
        _Roughness("Authored ground roughness",2D)="white"{}
        _Color("Authored linear tint",Color)=(1,1,1,1)
        _BumpScale("Authored normal strength",Float)=.35
        _HasRoughness("Real roughness map present",Float)=1
        _RoughnessConstant("Declared fallback roughness",Range(0,1))=.9
    }
    SubShader
    {
        Tags {"Queue"="Transparent-10" "RenderType"="Transparent"}
        Cull Back
        ZWrite Off
        CGPROGRAM
        #pragma surface surf Standard alpha:fade fullforwardshadows vertex:read_edge_opacity
        #pragma target 3.0
        #include "UnityStandardUtils.cginc"
        sampler2D _MainTex,_BumpMap,_Roughness;
        fixed4 _Color;
        half _BumpScale,_HasRoughness,_RoughnessConstant;
        struct Input {float2 uv_MainTex;float4 edge_color : COLOR;};
        void read_edge_opacity(inout appdata_full vertex,out Input output)
        {UNITY_INITIALIZE_OUTPUT(Input,output);output.edge_color=vertex.color;}
        void surf(Input input,inout SurfaceOutputStandard output)
        {
            output.Albedo=tex2D(_MainTex,input.uv_MainTex).rgb*_Color.rgb;
            output.Normal=UnpackScaleNormal(tex2D(_BumpMap,input.uv_MainTex),_BumpScale);
            output.Smoothness=1-lerp(_RoughnessConstant,tex2D(_Roughness,input.uv_MainTex).r,_HasRoughness);
            output.Metallic=0;output.Occlusion=1;
            output.Alpha=saturate(input.edge_color.a*_Color.a);
        }
        ENDCG
    }
    FallBack Off
}
