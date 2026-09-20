Shader "ZombieGame/CharacterAtlas"
{
    Properties { _MainTex ("Atlas", 2D) = "white" {} }
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
            struct appdata { float4 vertex:POSITION; float3 normal:NORMAL; float2 uv:TEXCOORD0; float4 color:COLOR; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct v2f { float4 vertex:SV_POSITION; float2 uv:TEXCOORD0; float light:TEXCOORD1; float4 color:COLOR; };
            v2f vert(appdata v)
            {
                UNITY_SETUP_INSTANCE_ID(v);
                v2f o; o.vertex=UnityObjectToClipPos(v.vertex); o.uv=v.uv;
                o.color=v.color;
                o.light=.55+.45*saturate(dot(UnityObjectToWorldNormal(v.normal),normalize(float3(-.4,.8,-.3))));
                return o;
            }
            fixed4 frag(v2f i):SV_Target { fixed4 color=tex2D(_MainTex,i.uv); color.rgb=lerp(color.rgb,i.color.rgb,i.color.a)*i.light; color.a=1; return color; }
            ENDCG
        }
    }
}
