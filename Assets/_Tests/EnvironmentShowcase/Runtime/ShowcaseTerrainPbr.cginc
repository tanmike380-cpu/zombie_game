#define TERRAIN_STANDARD_SHADER
#define TERRAIN_SURFACE_OUTPUT SurfaceOutputStandard
#include "UnityPBSLighting.cginc"
#define Input ShowcaseNativeTerrainInput
#define SplatmapVert ShowcaseNativeTerrainVert
#define SplatmapFinalColor ShowcaseNativeTerrainFinalColor
#define SplatmapFinalGBuffer ShowcaseNativeTerrainFinalGBuffer
#include "TerrainSplatmapCommon.cginc"
#undef Input
#undef SplatmapVert
#undef SplatmapFinalColor
#undef SplatmapFinalGBuffer

struct Input
{
    float4 tc;
    UNITY_FOG_COORDS(0)
    float3 terrain_position;
    float3 terrain_normal;
};

void SplatmapVert(inout appdata_full vertex, out Input data)
{
    ShowcaseNativeTerrainInput native_data;
    ShowcaseNativeTerrainVert(vertex, native_data);
    UNITY_INITIALIZE_OUTPUT(Input, data);
    data.tc = native_data.tc;
    data.terrain_position = vertex.vertex.xyz;
    data.terrain_normal = vertex.normal;
    UNITY_TRANSFER_FOG(data, UnityObjectToClipPos(vertex.vertex));
}

void SplatmapFinalColor(Input input, SurfaceOutputStandard output, inout fixed4 color)
{
    color *= output.Alpha;
    #ifdef TERRAIN_SPLAT_ADDPASS
        UNITY_APPLY_FOG_COLOR(input.fogCoord, color, fixed4(0,0,0,0));
    #else
        UNITY_APPLY_FOG(input.fogCoord, color);
    #endif
}

void SplatmapFinalGBuffer(Input input, SurfaceOutputStandard output, inout half4 first, inout half4 second, inout half4 third, inout half4 emission)
{
    UnityStandardDataApplyWeightToGbuffer(first, second, third, output.Alpha);
    emission *= output.Alpha;
}

half _Smoothness0, _Smoothness1, _Smoothness2, _Smoothness3;
float _ShowcaseTerrainSize, _MacroScale, _MacroStrength, _BreakupStrength;
float _DetailFadeStart, _DetailFadeEnd;
float4 _ShowcaseTerrainOrigin, _GrassTint, _DryTint, _SoilTint, _MudTint;
float4 _PlateauCenter, _PlateauRadius;
float _PlateauEnabled, _PlateauLedge, _ErosionStrength, _HasCliffPbr, _HasTalusPbr, _CliffTileSize, _TalusTileSize, _CliffNormalStrength;
// These approved maps share repeat/trilinear/aniso8 import settings. One Metal sampler, four real PBR textures.
UNITY_DECLARE_TEX2D(_CliffPbr);
UNITY_DECLARE_TEX2D_NOSAMPLER(_CliffNormal);
UNITY_DECLARE_TEX2D_NOSAMPLER(_TalusPbr);
UNITY_DECLARE_TEX2D_NOSAMPLER(_TalusNormal);

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

float showcase_talus(float2 world_xz)
{
    float2 delta = (world_xz - _PlateauCenter.xy) / max(_PlateauRadius.xy, float2(1,1));
    float angle = atan2(delta.y, delta.x);
    float radius = 1 + (sin(angle * 7) + sin(angle * 13) * .4) * _ErosionStrength;
    float physical_radius = max(min(_PlateauRadius.x, _PlateauRadius.y),
        length(delta * _PlateauRadius.xy) / max(length(delta), .0001));
    float distance = (length(delta) - radius) * physical_radius;
    // Desert's secondary weathered ledge moves its true outer foot outward.
    distance -= _PlateauLedge * _PlateauRadius.z * 2;
    return smoothstep(-.5, 1, distance) * (1 - smoothstep(1, _PlateauRadius.w, distance)) * _PlateauEnabled;
}

fixed4 showcase_triplanar_color(float3 position, float3 geometric_normal)
{
    float3 axes = pow(abs(geometric_normal), 4);
    axes /= max(.0001, axes.x + axes.y + axes.z);
    float3 orientation = step(0, geometric_normal) * 2 - 1;
    position /= max(.5, _CliffTileSize);
    return UNITY_SAMPLE_TEX2D(_CliffPbr, float2(-orientation.x * position.z, position.y)) * axes.x +
        UNITY_SAMPLE_TEX2D(_CliffPbr, float2(position.x, -orientation.y * position.z)) * axes.y +
        UNITY_SAMPLE_TEX2D(_CliffPbr, float2(orientation.z * position.x, position.y)) * axes.z;
}

half3 showcase_triplanar_normal(float3 position, float3 geometric_normal)
{
    float3 axes = pow(abs(geometric_normal), 4);
    axes /= max(.0001, axes.x + axes.y + axes.z);
    float3 orientation = step(0, geometric_normal) * 2 - 1;
    position /= max(.5, _CliffTileSize);
    half3 x_normal = UnpackNormalWithScale(UNITY_SAMPLE_TEX2D_SAMPLER(_CliffNormal, _CliffPbr, float2(-orientation.x * position.z, position.y)), _CliffNormalStrength);
    half3 y_normal = UnpackNormalWithScale(UNITY_SAMPLE_TEX2D_SAMPLER(_CliffNormal, _CliffPbr, float2(position.x, -orientation.y * position.z)), _CliffNormalStrength);
    half3 z_normal = UnpackNormalWithScale(UNITY_SAMPLE_TEX2D_SAMPLER(_CliffNormal, _CliffPbr, float2(orientation.z * position.x, position.y)), _CliffNormalStrength);
    // Blend tangent perturbations around the actual terrain normal; a flat source map leaves slope normals untouched.
    half3 world_normal = normalize(geometric_normal +
        half3(0, x_normal.y, -orientation.x * x_normal.x) * axes.x +
        half3(y_normal.x, 0, -orientation.y * y_normal.y) * axes.y +
        half3(orientation.z * z_normal.x, z_normal.y, 0) * axes.z);
    half3 tangent = normalize(cross(geometric_normal, half3(0,0,1)));
    half3 bitangent = normalize(cross(tangent, geometric_normal));
    return normalize(half3(dot(world_normal, tangent), dot(world_normal, bitangent), dot(world_normal, geometric_normal)));
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
    half3 geometric_normal = normalize(input.terrain_normal);
    float cliff_weight = smoothstep(.34, .72, 1 - abs(geometric_normal.y)) * _HasCliffPbr;
    float talus_weight = showcase_talus(world_xz) * _HasTalusPbr;
    float3 world_position = float3(world_xz.x, input.terrain_position.y + _ShowcaseTerrainOrigin.y, world_xz.y);
    #ifndef TERRAIN_SPLAT_ADDPASS
        color0.rgb *= _GrassTint.rgb;
        color1.rgb *= _DryTint.rgb;
        color2.rgb *= _SoilTint.rgb;
        color3.rgb *= _MudTint.rgb;
    #else
        // Road belongs to add-pass layer 1: keep coherent cobbles, no ghosted neighbours.
        color1 = tex2D(_Splat1, uv1);
        UNITY_BRANCH if (control.r > .01 && talus_weight > .001)
            color0 = lerp(color0, UNITY_SAMPLE_TEX2D_SAMPLER(_TalusPbr, _CliffPbr, world_xz / max(.5, _TalusTileSize)), talus_weight);
        UNITY_BRANCH if (control.r > .01 && cliff_weight > .001)
            color0 = lerp(color0, showcase_triplanar_color(world_position, geometric_normal), cliff_weight);
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
            UNITY_BRANCH if (control.r > .01 && talus_weight > .001)
                normal0 = normalize(lerp(normal0, UnpackNormalWithScale(UNITY_SAMPLE_TEX2D_SAMPLER(_TalusNormal, _CliffPbr, world_xz / max(.5, _TalusTileSize)), .3), talus_weight));
            UNITY_BRANCH if (control.r > .01 && cliff_weight > .001)
                normal0 = normalize(lerp(normal0, showcase_triplanar_normal(world_position, geometric_normal), cliff_weight));
        #endif
        half3 normal = normalize(normal0 * control.r + normal1 * control.g + normal2 * control.b + normal3 * control.a + half3(0,0,.00001));
        float detail_fade = 1 - smoothstep(_DetailFadeStart, _DetailFadeEnd, distance(world_xz, _WorldSpaceCameraPos.xz));
        output.Normal = normalize(lerp(half3(0,0,1), normal, detail_fade));
    #endif
}
