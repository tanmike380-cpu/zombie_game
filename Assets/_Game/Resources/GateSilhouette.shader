Shader "ZombieGame/GateSilhouette"
{
    SubShader
    {
        Tags {"Queue"="Transparent+10" "RenderType"="Transparent"}
        Pass
        {
            // Explicit friendly gate membership controls visibility. Render above the dithered
            // tower (and the unit's own depth), never for an enemy or a distant fogged unit.
            ZTest Always ZWrite Off Cull Off Blend SrcAlpha OneMinusSrcAlpha
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"
            struct appdata {float4 vertex:POSITION;float3 normal:NORMAL;UNITY_VERTEX_INPUT_INSTANCE_ID};
            float4 vert(appdata v):SV_POSITION {UNITY_SETUP_INSTANCE_ID(v);return UnityObjectToClipPos(v.vertex);}
            fixed4 frag():SV_Target{return fixed4(.18,.70,1,.65);}
            ENDCG
        }
    }
}
