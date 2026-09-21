Shader "ZombieGame/CharacterAtlas"
{
    Properties
    {
        _MainTex ("Atlas", 2D) = "white" {}
        _Saturation ("Saturation", Range(0,1)) = 1
        _Tint ("Tint", Color) = (1,1,1,1)
        _Ambient ("Ambient", Range(0.1,0.8)) = 0.55
        _Glossiness ("Metal finish", Range(0,1)) = .2
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
            CGPROGRAM
            #pragma surface surf Standard fullforwardshadows addshadow
            #pragma target 3.0
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"
            sampler2D _MainTex;
            float _Saturation, _Ambient, _Glossiness;
            float4 _Tint;
            struct Input { float2 uv_MainTex; float4 color:COLOR; float3 worldPos; };
            void surf(Input i,inout SurfaceOutputStandard o)
            {
                fixed4 color=tex2D(_MainTex,i.uv_MainTex);
                color.rgb=lerp(color.rgb,i.color.rgb,i.color.a);
                float grey=dot(color.rgb,float3(.2126,.7152,.0722));
                color.rgb=lerp(float3(grey,grey,grey),color.rgb,_Saturation)*_Tint.rgb;
                float wear=frac(sin(dot(floor(i.worldPos*70),float3(12.9,78.2,39.4)))*43758.54);
                float chroma=max(color.r,max(color.g,color.b))-min(color.r,min(color.g,color.b));
                float metal=(1-smoothstep(.04,.14,chroma))*smoothstep(.18,.4,grey);
                o.Albedo=color.rgb*(.96+.06*wear);o.Metallic=metal*.5;
                o.Smoothness=lerp(.08,_Glossiness,metal);o.Occlusion=.9;o.Alpha=1;
            }
            ENDCG
    }
}
