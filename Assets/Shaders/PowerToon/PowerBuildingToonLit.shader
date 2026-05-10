Shader "PowerSystem/Toon/BuildingLit"
{
    Properties
    {
        [Header(Base)]
        [MainTexture] _BaseMap("Base Map", 2D) = "white" {}
        [HDR][MainColor] _BaseColor("Base Color", Color) = (1, 1, 1, 1)
        [Enum(UnityEngine.Rendering.CullMode)] _Cull("Cull", Float) = 2

        [Header(Alpha Clipping)]
        [Toggle(_ALPHATEST_ON)] _AlphaClip("Enable", Float) = 0
        _Cutoff("Cutoff", Range(0, 1)) = 0.5

        [Header(Toon Lighting)]
        [ToggleUI] _UseFlatNormals("Use Flat Normals", Float) = 1
        _CelShadeMidPoint("Shadow Threshold", Range(-1, 1)) = -0.25
        _CelShadeSoftness("Shadow Softness", Range(0.001, 0.5)) = 0.05
        _CelSteps("Light Steps", Range(2, 4)) = 3
        _CelHardness("Step Hardness", Range(0, 1)) = 0.85
        _AmbientMinColor("Ambient Minimum", Color) = (0.12, 0.14, 0.16, 1)
        _IndirectLightMultiplier("Indirect Multiplier", Range(0, 2)) = 0.55
        _DirectLightMultiplier("Main Light Multiplier", Range(0, 2)) = 0.9
        _AdditionalLightMultiplier("Additional Light Multiplier", Range(0, 1)) = 0.25

        [Header(Shadow Style)]
        _ReceiveShadowMappingAmount("Shadow Map Strength", Range(0, 1)) = 0.75
        _ReceiveShadowMappingPosOffset("Shadow Bias Offset", Float) = 0
        _ShadowColor("Toon Shadow Color", Color) = (0.24, 0.30, 0.36, 1)
        _ShadowStrength("Toon Shadow Strength", Range(0, 1)) = 0.7
        [HDR] _TopLightColor("Top Face Tint", Color) = (1.0, 0.98, 0.9, 1)
        _TopLightStrength("Top Face Boost", Range(0, 1)) = 0.1

        [Header(Occlusion)]
        [ToggleUI] _UseOcclusion("Enable", Float) = 0
        _OcclusionStrength("Strength", Range(0, 1)) = 1
        [NoScaleOffset] _OcclusionMap("Occlusion Map", 2D) = "white" {}
        _OcclusionMapChannelMask("Channel Mask", Vector) = (1, 0, 0, 0)
        _OcclusionRemapStart("Remap Start", Range(0, 1)) = 0
        _OcclusionRemapEnd("Remap End", Range(0, 1)) = 1

        [Header(Emission)]
        [ToggleUI] _UseEmission("Enable", Float) = 0
        [HDR] _EmissionColor("Emission Color", Color) = (0, 0, 0, 1)
        _EmissionMulByBaseColor("Mul Base Color", Range(0, 1)) = 0
        [NoScaleOffset] _EmissionMap("Emission Map", 2D) = "white" {}
        _EmissionMapChannelMask("Channel Mask", Vector) = (1, 1, 1, 0)

        [Header(Outline)]
        _OutlineWidth("Width", Range(0, 5)) = 0.5
        _OutlineColor("Color", Color) = (0.08, 0.10, 0.12, 1)
        _OutlineZOffset("Z Offset", Range(0, 0.01)) = 0.0002
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Opaque"
            "Queue" = "Geometry"
            "IgnoreProjector" = "True"
            "UniversalMaterialType" = "ComplexLit"
        }

        LOD 100

        HLSLINCLUDE
        #pragma shader_feature_local_fragment _ALPHATEST_ON
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForwardOnly" }

            Blend One Zero
            ZWrite On
            ZTest LEqual
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex PowerToonVertex
            #pragma fragment PowerToonFragment

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ EVALUATE_SH_MIXED EVALUATE_SH_VERTEX
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_fragment _ _DBUFFER_MRT1 _DBUFFER_MRT2 _DBUFFER_MRT3
            #pragma multi_compile_fragment _ _LIGHT_LAYERS
            #pragma multi_compile_fragment _ _LIGHT_COOKIES
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RenderingLayers.hlsl"
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Fog.hlsl"

            #pragma multi_compile _ LIGHTMAP_SHADOW_MIXING
            #pragma multi_compile _ SHADOWS_SHADOWMASK
            #pragma multi_compile_fragment _ LOD_FADE_CROSSFADE
            #pragma multi_compile_fragment _ DEBUG_DISPLAY

            #pragma multi_compile_instancing
            #pragma instancing_options renderinglayer
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"

            #include "PowerToonCommon.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "Outline"

            Blend One Zero
            ZWrite On
            ZTest LEqual
            Cull Front

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex PowerToonVertex
            #pragma fragment PowerToonFragment

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ EVALUATE_SH_MIXED EVALUATE_SH_VERTEX
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_fragment _ _DBUFFER_MRT1 _DBUFFER_MRT2 _DBUFFER_MRT3
            #pragma multi_compile_fragment _ _LIGHT_LAYERS
            #pragma multi_compile_fragment _ _LIGHT_COOKIES
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RenderingLayers.hlsl"
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Fog.hlsl"

            #pragma multi_compile_fragment _ LOD_FADE_CROSSFADE
            #pragma multi_compile_fragment _ DEBUG_DISPLAY

            #pragma multi_compile_instancing
            #pragma instancing_options renderinglayer
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"

            #define POWER_TOON_OUTLINE
            #include "PowerToonCommon.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex PowerToonVertex
            #pragma fragment PowerToonDepthFragment

            #pragma multi_compile_instancing
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"
            #pragma multi_compile_fragment _ LOD_FADE_CROSSFADE
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            #define POWER_TOON_SHADOWCASTER
            #include "PowerToonCommon.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            ZWrite On
            ZTest LEqual
            ColorMask R
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex PowerToonVertex
            #pragma fragment PowerToonDepthOnlyFragment

            #pragma multi_compile_fragment _ LOD_FADE_CROSSFADE
            #pragma multi_compile_instancing
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"

            #include "PowerToonCommon.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormalsOnly"
            Tags { "LightMode" = "DepthNormalsOnly" }

            ZWrite On
            ZTest LEqual
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex PowerToonVertex
            #pragma fragment PowerToonDepthNormalsFragment

            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RenderingLayers.hlsl"
            #pragma multi_compile_fragment _ LOD_FADE_CROSSFADE
            #pragma multi_compile_instancing
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"

            #include "PowerToonCommon.hlsl"
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
