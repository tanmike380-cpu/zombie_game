Shader "ZombieGame/CharacterAtlas"
{
    Properties
    {
        _MainTex ("Atlas", 2D) = "white" {}
        _Saturation ("Saturation", Range(0,1)) = 1
        _Tint ("Tint", Color) = (1,1,1,1)
        _Ambient ("Ambient", Range(0.1,0.8)) = 0.55
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"
            sampler2D _MainTex;
            float _Saturation, _Ambient;
            float4 _Tint;
            struct appdata { float4 vertex:POSITION; float3 normal:NORMAL; float2 uv:TEXCOORD0; float4 color:COLOR; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct v2f { float4 vertex:SV_POSITION; float2 uv:TEXCOORD0; float light:TEXCOORD1; float4 color:COLOR; float3 local:TEXCOORD2; };
            v2f vert(appdata v)
            {
                UNITY_SETUP_INSTANCE_ID(v);
                v2f o; o.vertex=UnityObjectToClipPos(v.vertex); o.uv=v.uv;
                o.color=v.color;o.local=v.vertex.xyz;
                o.light=_Ambient+(1-_Ambient)*saturate(dot(UnityObjectToWorldNormal(v.normal),normalize(float3(-.4,.8,-.3))));
                return o;
            }
            fixed4 frag(v2f i):SV_Target
            {
                fixed4 color=tex2D(_MainTex,i.uv);
                color.rgb=lerp(color.rgb,i.color.rgb,i.color.a);
                float grey=dot(color.rgb,float3(.2126,.7152,.0722));
                color.rgb=lerp(float3(grey,grey,grey),color.rgb,_Saturation)*_Tint.rgb*i.light;
                float wear=frac(sin(dot(floor(i.local*95),float3(12.9,78.2,39.4)))*43758.54);
                color.rgb*=.90+.13*wear;
                color.a=1;
                return color;
            }
            ENDCG
        }
    }
}
