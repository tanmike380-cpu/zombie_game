Shader "ZombieGame/ImportedColour"
{
    Properties { _MainTex("Original baked colour",2D)="white"{} _Color("Colour",Color)=(1,1,1,1) }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        Pass
        {
            Cull Off
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"
            sampler2D _MainTex; float4 _MainTex_ST; fixed4 _Color;
            struct input_data { float4 vertex:POSITION; float2 uv:TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct output_data { float4 vertex:SV_POSITION; float2 uv:TEXCOORD0; };
            output_data vert(input_data v) { UNITY_SETUP_INSTANCE_ID(v); output_data o; o.vertex=UnityObjectToClipPos(v.vertex); o.uv=TRANSFORM_TEX(v.uv,_MainTex); return o; }
            fixed4 frag(output_data i):SV_Target { return tex2D(_MainTex,i.uv)*_Color; }
            ENDCG
        }
    }
}
