Shader "ZombieGame/GreekFlame"
{
    SubShader
    {
        Tags { "Queue"="Transparent+5" "RenderType"="Transparent" }
        Blend SrcAlpha One
        ZWrite Off Cull Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            struct appdata { float4 vertex:POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; };
            struct v2f { float4 vertex:SV_POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; float3 world:TEXCOORD1; };
            v2f vert(appdata v)
            {
                v2f o; o.vertex=UnityObjectToClipPos(v.vertex);o.color=v.color;o.uv=v.uv;
                o.world=mul(unity_ObjectToWorld,v.vertex).xyz;return o;
            }
            float hash(float3 p) { return frac(sin(dot(p,float3(12.98,78.23,39.42)))*43758.54); }
            float noise(float3 p)
            {
                float3 b=floor(p), f=frac(p);f=f*f*(3-2*f);
                return lerp(lerp(lerp(hash(b),hash(b+float3(1,0,0)),f.x),lerp(hash(b+float3(0,1,0)),hash(b+float3(1,1,0)),f.x),f.y),
                    lerp(lerp(hash(b+float3(0,0,1)),hash(b+float3(1,0,1)),f.x),lerp(hash(b+float3(0,1,1)),hash(b+1),f.x),f.y),f.z);
            }
            float4 frag(v2f i):SV_Target
            {
                float2 p=i.uv*2-1;
                float edge=saturate(1-dot(p,p));
                float turbulence=noise(i.world*5-float3(0,_Time.y*9,_Time.y*6));
                float flame=saturate(edge*1.8-.48+turbulence*.5);
                return float4(i.color.rgb*1.8,i.color.a*flame*edge);
            }
            ENDCG
        }
    }
}
