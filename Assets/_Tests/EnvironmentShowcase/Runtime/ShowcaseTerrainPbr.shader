Shader "ZombieGame/EnvironmentShowcase/TerrainPbr"
{
    Properties
    {
        [HideInInspector] _Control("Control", 2D) = "red" {}
        [HideInInspector] _Splat0("Layer 0", 2D) = "white" {}
        [HideInInspector] _Splat1("Layer 1", 2D) = "white" {}
        [HideInInspector] _Splat2("Layer 2", 2D) = "white" {}
        [HideInInspector] _Splat3("Layer 3", 2D) = "white" {}
        [HideInInspector] _Normal0("Normal 0", 2D) = "bump" {}
        [HideInInspector] _Normal1("Normal 1", 2D) = "bump" {}
        [HideInInspector] _Normal2("Normal 2", 2D) = "bump" {}
        [HideInInspector] _Normal3("Normal 3", 2D) = "bump" {}
        [HideInInspector] _TerrainHolesTexture("Holes", 2D) = "white" {}
        [HideInInspector] _Smoothness0("Smoothness 0", Range(0,1)) = 1
        [HideInInspector] _Smoothness1("Smoothness 1", Range(0,1)) = 1
        [HideInInspector] _Smoothness2("Smoothness 2", Range(0,1)) = 1
        [HideInInspector] _Smoothness3("Smoothness 3", Range(0,1)) = 1
        _ShowcaseTerrainSize("Ground size", Float) = 128
        _ShowcaseTerrainOrigin("Ground origin", Vector) = (-64,0,-64,0)
        _MacroScale("Macro metres", Float) = 24
        _MacroStrength("Macro contrast", Range(0,1)) = .22
        _BreakupStrength("Neighbour blend", Range(0,1)) = .52
        _DetailFadeStart("Detail fade start", Float) = 28
        _DetailFadeEnd("Detail fade end", Float) = 100
        _CliffPbr("Approved cliff RGB + smoothness", 2D) = "white" {}
        _CliffNormal("Approved cliff normal GL", 2D) = "bump" {}
        _TalusPbr("Approved talus RGB + smoothness", 2D) = "white" {}
        _TalusNormal("Approved talus normal GL", 2D) = "bump" {}
        _PlateauCenter("Plateau world centre XZ", Vector) = (41,47,0,0)
        _PlateauRadius("Plateau radii / edge / talus", Vector) = (18,11,2.2,6)
        _PlateauEnabled("Plateau enabled", Float) = 0
        _PlateauLedge("Secondary weathered ledge", Float) = 0
        _ErosionStrength("Weathered rim", Float) = 0
        _HasCliffPbr("Verified cliff PBR present", Float) = 0
        _HasTalusPbr("Verified talus PBR present", Float) = 0
        _CliffTileSize("Cliff metres", Float) = 4
        _TalusTileSize("Talus metres", Float) = 2.5
        _CliffNormalStrength("Cliff normal strength", Float) = .3
        _GrassTint("Grass grade", Color) = (.69,.86,.54,1)
        _DryTint("Dry grade", Color) = (.81,.81,.62,1)
        _SoilTint("Soil grade", Color) = (.86,.82,.73,1)
        _MudTint("Mud grade", Color) = (.76,.83,.74,1)
    }
    SubShader
    {
        Tags { "Queue"="Geometry-100" "RenderType"="Opaque" "TerrainCompatible"="True" }
        CGPROGRAM
        #pragma surface surf Standard vertex:SplatmapVert finalcolor:SplatmapFinalColor finalgbuffer:SplatmapFinalGBuffer addshadow fullforwardshadows
        #pragma target 3.0
        #pragma instancing_options assumeuniformscaling nomatrices nolightprobe nolightmap
        #pragma multi_compile_fog
        #pragma shader_feature_local _NORMALMAP
        #pragma shader_feature_local _ALPHATEST_ON
        #include "ShowcaseTerrainPbr.cginc"
        ENDCG
    }
    Dependency "AddPassShader" = "Hidden/ZombieGame/EnvironmentShowcase/TerrainAddPbr"
    Dependency "BaseMapShader" = "Hidden/TerrainEngine/Splatmap/Standard-Base"
    Dependency "BaseMapGenShader" = "Hidden/TerrainEngine/Splatmap/Standard-BaseGen"
    FallBack "Nature/Terrain/Standard"
}
