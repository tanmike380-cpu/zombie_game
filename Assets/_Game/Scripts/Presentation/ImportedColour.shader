Shader "ZombieGame/ImportedColour"
{
    Properties {
        _MainTex("Original baked colour",2D)="white"{} _Color("Colour",Color)=(1,1,1,1)
        _Glossiness("Surface smoothness",Range(0,1))=.18
        _BakedLight("Preserve source baked illumination",Range(0,1))=.45
        _Metallic("Metallic",Range(0,1))=0
        _GateVisibility("Friendly passage visibility",Range(0,1))=1
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        Cull Off
        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows addshadow
        #pragma target 3.0
        #pragma multi_compile_instancing
        sampler2D _MainTex; fixed4 _Color; half _Glossiness,_BakedLight,_Metallic,_GateVisibility;
        struct Input {float2 uv_MainTex;float4 screenPos;};
        void surf(Input i,inout SurfaceOutputStandard o)
        {
            float2 pixel=floor(i.screenPos.xy/i.screenPos.w*_ScreenParams.xy);
            clip(_GateVisibility-frac(dot(pixel,float2(.754877666,.569840296))));
            fixed3 colour=tex2D(_MainTex,i.uv_MainTex).rgb*_Color.rgb;
            // Existing Tripo maps include illumination. Partial emission avoids double-black
            // shadows while allowing real contact shadows, surface lighting and reflection.
            o.Albedo=colour*(1-_BakedLight);o.Emission=colour*_BakedLight;
            o.Metallic=_Metallic;o.Smoothness=_Glossiness;o.Alpha=1;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
