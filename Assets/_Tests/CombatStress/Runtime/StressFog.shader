Shader "ZombieGame/StressFog"
{
    Properties { _MainTex ("Fog", 2D) = "black" {} }
    SubShader
    {
        Tags { "Queue"="Transparent+50" "RenderType"="Transparent" }
        Cull Off ZWrite Off ZTest Always
        Blend SrcAlpha OneMinusSrcAlpha
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _MainTex;
            struct input_data { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
            struct vertex_data { float4 vertex : SV_POSITION; float2 uv : TEXCOORD0; };
            vertex_data vert(input_data input)
            { vertex_data result; result.vertex = UnityObjectToClipPos(input.vertex); result.uv = input.uv; return result; }
            fixed4 frag(vertex_data input) : SV_Target { return tex2D(_MainTex, input.uv); }
            ENDCG
        }
    }
}
