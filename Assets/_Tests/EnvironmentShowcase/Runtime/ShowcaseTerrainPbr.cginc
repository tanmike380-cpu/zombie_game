#define TERRAIN_STANDARD_SHADER
#define TERRAIN_SURFACE_OUTPUT SurfaceOutputStandard
#include "UnityPBSLighting.cginc"
#include "TerrainSplatmapCommon.cginc"

half _Smoothness0, _Smoothness1, _Smoothness2, _Smoothness3;
float _ShowcaseTerrainSize, _MacroScale, _MacroStrength, _BreakupStrength;
float _DetailFadeStart, _DetailFadeEnd;
float4 _ShowcaseTerrainOrigin, _GrassTint, _DryTint, _SoilTint, _MudTint;

float showcase_hash(float2 value)
{
    return frac(sin(dot(value, float2(127.1, 311.7))) * 43758.5453);
}

float showcase_noise(float2 value)
{
    float2 cell = floor(value);
    float2 fraction = frac(value);
    fraction = fraction * fraction * (3 - 2 * fraction);
    return lerp(lerp(showcase_hash(cell), showcase_hash(cell + float2(1,0)), fraction.x),
        lerp(showcase_hash(cell + float2(0,1)), showcase_hash(cell + 1), fraction.x), fraction.y);
}

fixed4 showcase_sample(sampler2D source, float2 uv, float blend)
{
    // Offset-neighbour crossfade, not random cell seams or rotated normal channels.
    return lerp(tex2D(source, uv), tex2D(source, uv + float2(17.37, 31.13)), blend);
}

half3 showcase_normal(sampler2D source, float2 uv, half scale, float blend)
{
    return normalize(lerp(UnpackNormalWithScale(tex2D(source, uv), scale),
        UnpackNormalWithScale(tex2D(source, uv + float2(17.37, 31.13)), scale), blend));
}

void surf(Input input, inout SurfaceOutputStandard output)
{
    #ifdef _ALPHATEST_ON
        ClipHoles(input.tc.xy);
    #endif
    float2 control_uv = (input.tc.xy * (_Control_TexelSize.zw - 1) + .5) * _Control_TexelSize.xy;
    half4 control = tex2D(_Control, control_uv);
    half weight = dot(control, half4(1,1,1,1));
    #ifdef TERRAIN_SPLAT_ADDPASS
        clip(weight - .0001);
    #endif
    control /= max(weight, .0001);
    float2 world_xz = input.tc.xy * _ShowcaseTerrainSize + _ShowcaseTerrainOrigin.xz;
    float macro_noise = showcase_noise(world_xz / max(1, _MacroScale));
    float blend = showcase_noise(world_xz / 9 + 13.2) * _BreakupStrength;
    float macro_multiplier = 1 + (macro_noise - .5) * _MacroStrength;
    float2 uv0 = TRANSFORM_TEX(input.tc.xy, _Splat0);
    float2 uv1 = TRANSFORM_TEX(input.tc.xy, _Splat1);
    float2 uv2 = TRANSFORM_TEX(input.tc.xy, _Splat2);
    float2 uv3 = TRANSFORM_TEX(input.tc.xy, _Splat3);
    fixed4 color0 = showcase_sample(_Splat0, uv0, blend);
    fixed4 color1 = showcase_sample(_Splat1, uv1, blend);
    fixed4 color2 = showcase_sample(_Splat2, uv2, blend);
    fixed4 color3 = showcase_sample(_Splat3, uv3, blend);
    #ifndef TERRAIN_SPLAT_ADDPASS
        color0.rgb *= _GrassTint.rgb;
        color1.rgb *= _DryTint.rgb;
        color2.rgb *= _SoilTint.rgb;
        color3.rgb *= _MudTint.rgb;
    #else
        // Road belongs to add-pass layer 1: keep coherent cobbles, no ghosted neighbours.
        color1 = tex2D(_Splat1, uv1);
    #endif
    output.Albedo = (color0.rgb * control.r + color1.rgb * control.g + color2.rgb * control.b + color3.rgb * control.a) * macro_multiplier;
    output.Smoothness = dot(half4(color0.a, color1.a, color2.a, color3.a) *
        half4(_Smoothness0, _Smoothness1, _Smoothness2, _Smoothness3), control);
    output.Metallic = 0;
    output.Occlusion = 1;
    output.Alpha = weight;
    #ifdef _NORMALMAP
        half3 normal0 = showcase_normal(_Normal0, uv0, _NormalScale0, blend);
        half3 normal1 = showcase_normal(_Normal1, uv1, _NormalScale1, blend);
        half3 normal2 = showcase_normal(_Normal2, uv2, _NormalScale2, blend);
        half3 normal3 = showcase_normal(_Normal3, uv3, _NormalScale3, blend);
        #ifdef TERRAIN_SPLAT_ADDPASS
            normal1 = UnpackNormalWithScale(tex2D(_Normal1, uv1), _NormalScale1);
        #endif
        half3 normal = normalize(normal0 * control.r + normal1 * control.g + normal2 * control.b + normal3 * control.a + half3(0,0,.00001));
        float detail_fade = 1 - smoothstep(_DetailFadeStart, _DetailFadeEnd, distance(world_xz, _WorldSpaceCameraPos.xz));
        output.Normal = normalize(lerp(half3(0,0,1), normal, detail_fade));
    #endif
}
