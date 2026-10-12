Shader "ZombieGame/EnvironmentShowcase/ConceptWater"
{
    Properties
    {
        _HeightField("Actual normalized Terrain heights",2D)="black"{}
        _Deep("Deep water",Color)=(.035,.17,.28,1)
        _Shallow("Shallow water",Color)=(.15,.43,.46,1)
        _TerrainOrigin("Terrain origin",Vector)=(0,0,0,0)
        _TerrainSize("Terrain metres",Float)=128
        _HeightRange("Height metres",Float)=18
        _WaterLevel("Independent surface metres",Float)=1.1
        _IsPond("Finite closed pool",Float)=0
        _PoolCenter("Pool world center",Vector)=(0,0,0,0)
        _PoolRadius("Pool radii metres",Vector)=(15,11,0,0)
        _CoastX("Local coastline metres",Float)=32
    }
    SubShader
    {
        Tags {"RenderType"="Opaque" "Queue"="Geometry+10"}
        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows
        #pragma target 3.0
        sampler2D _HeightField;
        float4 _TerrainOrigin,_PoolCenter,_PoolRadius;
        half4 _Deep,_Shallow;
        float _TerrainSize,_HeightRange,_WaterLevel,_IsPond,_CoastX;
        struct Input {float3 worldPos;};
        void surf(Input input,inout SurfaceOutputStandard output)
        {
            float2 uv=(input.worldPos.xz-_TerrainOrigin.xz)/_TerrainSize;
            float depth=_WaterLevel-tex2D(_HeightField,uv).r*_HeightRange;
            if(uv.x<0||uv.y<0||uv.x>1||uv.y>1)
            {
                depth=4;
                if(_IsPond<.5) clip(_CoastX-(input.worldPos.x-_TerrainOrigin.x));
            }
            clip(depth-.012);
            float phase=dot(input.worldPos.xz,float2(3.9,2.57))+sin(dot(input.worldPos.xz,float2(.71,.83)))*1.7-_Time.y*.42;
            float second_phase=dot(input.worldPos.xz,float2(-2.19,1.11))-_Time.y*.23;
            float ripple=sin(phase)*.65+sin(second_phase)*.35;
            output.Albedo=lerp(_Shallow.rgb,_Deep.rgb,saturate(depth/(_IsPond>.5?1.0:.85)))*(1+ripple*.05);
            float shore=(1-smoothstep(.04,.22,depth))*(.5+.5*sin(phase*2));
            output.Albedo=lerp(output.Albedo,half3(.48,.57,.53),shore*(_IsPond>.5?.08:.3));
            output.Normal=normalize(half3(cos(phase)*.035,cos(second_phase)*.025,1));
            output.Smoothness=.4; output.Metallic=0; output.Occlusion=1; output.Alpha=1;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
