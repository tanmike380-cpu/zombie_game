Shader "ZombieGame/FrontierSurface"
{
    Properties {
        _Color ("Earth palette", Color) = (0.4,0.35,0.22,1)
        _Glossiness ("Roughness response", Range(0,1)) = 0.12
        _SurfaceKind("Ground Wood Stone Roof Water Other",Float)=5
        _MainTex("Scanned albedo",2D)="white"{}
        _RoughTex("Scanned roughness",2D)="white"{}
        _BumpMap("Scanned normal",2D)="bump"{}
        _TextureMode("Off / World projection / Model UV",Float)=0
        _TextureScale("World tile size inverse",Float)=0.2
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows
        #pragma target 3.0
        fixed4 _Color; half _Glossiness;float _SurfaceKind,_TextureMode,_TextureScale;
        sampler2D _MainTex,_RoughTex,_BumpMap;
        struct Input { float3 worldPos; float3 worldNormal; float2 uv_MainTex; INTERNAL_DATA };
        float hash(float2 p) { return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453); }
        float noise(float2 p)
        {
            float2 i=floor(p),f=frac(p);f=f*f*(3-2*f);
            return lerp(lerp(hash(i),hash(i+float2(1,0)),f.x),lerp(hash(i+float2(0,1)),hash(i+1),f.x),f.y);
        }
        void surf(Input i,inout SurfaceOutputStandard o)
        {
            float3 world_normal=WorldNormalVector(i,float3(0,0,1));
            float2 p=abs(world_normal.y)>.6?i.worldPos.xz:abs(world_normal.x)>.6?i.worldPos.zy:i.worldPos.xy;
            if(_TextureMode>.5)
            {
                float2 uv=_TextureMode>1.5?i.uv_MainTex:p*_TextureScale;
                o.Albedo=tex2D(_MainTex,uv).rgb*_Color.rgb;
                o.Smoothness=(1-tex2D(_RoughTex,uv).r)*.35;
                o.Metallic=0;o.Occlusion=1;
                if(_TextureMode>1.5)o.Normal=UnpackNormal(tex2D(_BumpMap,uv));
                return;
            }
            float broad=noise(p*.32),detail=noise(p*9),grain=noise(p*43);
            float variation=.86+broad*.22+detail*.08;
            float ground=_SurfaceKind<.5?1:0;
            float patches=noise(p*.15)*noise(p*1.9);
            float3 dirt=lerp(_Color.rgb,float3(.27,.215,.125),ground*smoothstep(.28,.57,patches)*.18);
            if(_SurfaceKind>.5&&_SurfaceKind<1.5)variation*=.80+.20*noise(float2(p.x*22,p.y*.65));
            if(_SurfaceKind>1.5&&_SurfaceKind<3.5)
            {
                float2 coordinates=float2(p.x*(_SurfaceKind>2.5?3.2:1.3)+floor(p.y*2)*.5,p.y*2);
                float2 tile=frac(coordinates);
                float filter_width=max(fwidth(coordinates.x),fwidth(coordinates.y));
                float mortar=smoothstep(.025,.075+filter_width,min(tile.x,tile.y));
                float detail_strength=1-smoothstep(.2,.7,filter_width);
                variation*=lerp(1,lerp(.72,1,mortar),detail_strength);
            }
            if(_SurfaceKind>3.5&&_SurfaceKind<4.5)
            {variation=.88+.12*sin(p.x*2+p.y*3+_Time.y*.8);dirt=_Color.rgb;}
            o.Albedo=dirt*variation;
            o.Metallic=0;o.Smoothness=_Glossiness;o.Occlusion=.9+.1*detail;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
