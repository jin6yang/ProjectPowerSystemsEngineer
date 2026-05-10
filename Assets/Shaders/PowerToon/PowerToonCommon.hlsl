#pragma once

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/LODCrossFade.hlsl"

TEXTURE2D(_BaseMap);
SAMPLER(sampler_BaseMap);
TEXTURE2D(_EmissionMap);
SAMPLER(sampler_EmissionMap);
TEXTURE2D(_OcclusionMap);
SAMPLER(sampler_OcclusionMap);

CBUFFER_START(UnityPerMaterial)
    float4 _BaseMap_ST;
    half4 _BaseColor;
    half _Cutoff;

    half _UseFlatNormals;
    half _CelShadeMidPoint;
    half _CelShadeSoftness;
    half _CelSteps;
    half _CelHardness;

    half3 _AmbientMinColor;
    half _IndirectLightMultiplier;
    half _DirectLightMultiplier;
    half _AdditionalLightMultiplier;
    half _ReceiveShadowMappingAmount;
    float _ReceiveShadowMappingPosOffset;

    half3 _ShadowColor;
    half _ShadowStrength;
    half3 _TopLightColor;
    half _TopLightStrength;

    half _UseOcclusion;
    half _OcclusionStrength;
    half4 _OcclusionMapChannelMask;
    half _OcclusionRemapStart;
    half _OcclusionRemapEnd;

    half _UseEmission;
    half3 _EmissionColor;
    half _EmissionMulByBaseColor;
    half3 _EmissionMapChannelMask;

    float _OutlineWidth;
    half3 _OutlineColor;
    float _OutlineZOffset;
CBUFFER_END

float3 _LightDirection;

struct PowerToonAttributes
{
    float3 positionOS : POSITION;
    half3 normalOS : NORMAL;
    half4 tangentOS : TANGENT;
    float2 uv : TEXCOORD0;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct PowerToonVaryings
{
    float2 uv : TEXCOORD0;
    float4 positionWSAndFog : TEXCOORD1;
    half3 normalWS : TEXCOORD2;
    float4 positionCS : SV_POSITION;
    UNITY_VERTEX_INPUT_INSTANCE_ID
    UNITY_VERTEX_OUTPUT_STEREO
};

struct PowerToonSurfaceData
{
    half3 albedo;
    half alpha;
    half occlusion;
    half3 emission;
};

float PowerToonInvLerpClamp(float from, float to, float value)
{
    return saturate((value - from) / max(0.0001, to - from));
}

float PowerToonGetCameraFOV()
{
    float t = unity_CameraProjection._m11;
    return atan(1.0 / t) * 2.0 * 57.29578;
}

float PowerToonOutlineScale(float positionVSZ)
{
    float cameraScale;

    if (unity_OrthoParams.w == 0)
    {
        cameraScale = saturate(abs(positionVSZ)) * PowerToonGetCameraFOV();
    }
    else
    {
        cameraScale = saturate(abs(unity_OrthoParams.y)) * 50.0;
    }

    return cameraScale * 0.00005;
}

float4 PowerToonApplyZOffset(float4 positionCS, float viewSpaceZOffset)
{
    if (unity_OrthoParams.w == 0)
    {
        float2 projZ = UNITY_MATRIX_P[2].zw;
        float modifiedPositionVSZ = -positionCS.w - viewSpaceZOffset;
        float modifiedPositionCSZ = modifiedPositionVSZ * projZ.x + projZ.y;
        positionCS.z = modifiedPositionCSZ * positionCS.w / (-modifiedPositionVSZ);
    }
    else
    {
        positionCS.z += -viewSpaceZOffset / _ProjectionParams.z;
    }

    return positionCS;
}

PowerToonVaryings PowerToonVertex(PowerToonAttributes input)
{
    PowerToonVaryings output;

    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_TRANSFER_INSTANCE_ID(input, output);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

    VertexPositionInputs positionInput = GetVertexPositionInputs(input.positionOS);
    VertexNormalInputs normalInput = GetVertexNormalInputs(input.normalOS, input.tangentOS);

    float3 positionWS = positionInput.positionWS;

#if defined(POWER_TOON_OUTLINE)
    float outlineAmount = _OutlineWidth * PowerToonOutlineScale(positionInput.positionVS.z);
    positionWS += normalInput.normalWS * outlineAmount;
#endif

    output.positionCS = TransformWorldToHClip(positionWS);

#if defined(POWER_TOON_OUTLINE)
    output.positionCS = PowerToonApplyZOffset(output.positionCS, _OutlineZOffset);
#endif

#if defined(POWER_TOON_SHADOWCASTER)
    float4 shadowPositionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalInput.normalWS, _LightDirection));

    #if UNITY_REVERSED_Z
        shadowPositionCS.z = min(shadowPositionCS.z, shadowPositionCS.w * UNITY_NEAR_CLIP_VALUE);
    #else
        shadowPositionCS.z = max(shadowPositionCS.z, shadowPositionCS.w * UNITY_NEAR_CLIP_VALUE);
    #endif

    output.positionCS = shadowPositionCS;
#endif

    output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
    output.normalWS = normalInput.normalWS;
    output.positionWSAndFog = float4(positionWS, ComputeFogFactor(output.positionCS.z));

    return output;
}

half4 PowerToonSampleBase(PowerToonVaryings input)
{
    return SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv) * _BaseColor;
}

void PowerToonAlphaClip(half alpha)
{
#if defined(_ALPHATEST_ON)
    clip(alpha - _Cutoff);
#endif
}

half PowerToonSampleOcclusion(PowerToonVaryings input)
{
    half occlusion = 1.0;

    if (_UseOcclusion > 0.5)
    {
        half4 sampleValue = SAMPLE_TEXTURE2D(_OcclusionMap, sampler_OcclusionMap, input.uv);
        half maskedValue = dot(sampleValue, _OcclusionMapChannelMask);
        maskedValue = lerp(1.0, maskedValue, _OcclusionStrength);
        occlusion = PowerToonInvLerpClamp(_OcclusionRemapStart, _OcclusionRemapEnd, maskedValue);
    }

    return occlusion;
}

half3 PowerToonSampleEmission(PowerToonVaryings input, half3 albedo)
{
    half3 emission = 0.0;

    if (_UseEmission > 0.5)
    {
        half3 emissionMask = SAMPLE_TEXTURE2D(_EmissionMap, sampler_EmissionMap, input.uv).rgb * _EmissionMapChannelMask;
        emission = emissionMask * _EmissionColor;
        emission = lerp(emission, emission * albedo, _EmissionMulByBaseColor);
    }

    return emission;
}

PowerToonSurfaceData PowerToonInitializeSurface(PowerToonVaryings input)
{
    PowerToonSurfaceData surfaceData;
    half4 baseColor = PowerToonSampleBase(input);

    surfaceData.albedo = baseColor.rgb;
    surfaceData.alpha = baseColor.a;
    PowerToonAlphaClip(surfaceData.alpha);

    surfaceData.occlusion = PowerToonSampleOcclusion(input);
    surfaceData.emission = PowerToonSampleEmission(input, surfaceData.albedo);

    return surfaceData;
}

half3 PowerToonGetNormalWS(PowerToonVaryings input)
{
    half3 normalWS = normalize(input.normalWS);

    if (_UseFlatNormals > 0.5)
    {
        float3 dpdx = ddx(input.positionWSAndFog.xyz);
        float3 dpdy = ddy(input.positionWSAndFog.xyz);
        half3 flatNormalWS = normalize(cross(dpdy, dpdx));

        if (dot(flatNormalWS, normalWS) < 0.0)
        {
            flatNormalWS = -flatNormalWS;
        }

        normalWS = flatNormalWS;
    }

    return normalWS;
}

half PowerToonQuantizeLight(half lightAmount)
{
    half steps = max(2.0, _CelSteps);
    half quantized = floor(saturate(lightAmount) * (steps - 0.0001)) / max(1.0, steps - 1.0);
    return lerp(lightAmount, quantized, _CelHardness);
}

half3 PowerToonShadeSingleLight(half3 normalWS, float3 positionWS, PowerToonSurfaceData surfaceData, Light light, half lightMultiplier)
{
    half noL = dot(normalWS, light.direction);
    half lightAmount = smoothstep(_CelShadeMidPoint - _CelShadeSoftness, _CelShadeMidPoint + _CelShadeSoftness, noL);

    half shadowAttenuation = lerp(1.0, light.shadowAttenuation, _ReceiveShadowMappingAmount);
    lightAmount *= shadowAttenuation;
    lightAmount *= surfaceData.occlusion;

    half topMask = saturate(normalWS.y);
    lightAmount = saturate(lightAmount + topMask * _TopLightStrength);
    lightAmount = PowerToonQuantizeLight(lightAmount);

    half3 shadowTint = lerp(1.0, _ShadowColor, _ShadowStrength);
    half3 toonTone = lerp(shadowTint, 1.0, lightAmount);
    toonTone *= lerp(1.0, _TopLightColor, topMask * _TopLightStrength);

    return saturate(light.color) * toonTone * light.distanceAttenuation * lightMultiplier;
}

half3 PowerToonShadeGI(half3 normalWS, PowerToonSurfaceData surfaceData)
{
    half3 ambient = SampleSH(normalWS);
    ambient = max(ambient, _AmbientMinColor);
    ambient *= lerp(1.0, surfaceData.occlusion, 0.5);
    return ambient * _IndirectLightMultiplier;
}

half3 PowerToonShadeAllLights(PowerToonVaryings input, PowerToonSurfaceData surfaceData, half3 normalWS)
{
    float3 positionWS = input.positionWSAndFog.xyz;
    float3 shadowTestPositionWS = positionWS;

    half3 indirect = PowerToonShadeGI(normalWS, surfaceData);
    half3 direct = 0.0;

    float4 shadowCoord = TransformWorldToShadowCoord(positionWS);
    Light mainLight = GetMainLight(shadowCoord);
    shadowTestPositionWS = positionWS + mainLight.direction * _ReceiveShadowMappingPosOffset;

#if defined(_MAIN_LIGHT_SHADOWS)
    float4 offsetShadowCoord = TransformWorldToShadowCoord(shadowTestPositionWS);
    mainLight.shadowAttenuation = MainLightRealtimeShadow(offsetShadowCoord);
#endif

    direct += PowerToonShadeSingleLight(normalWS, positionWS, surfaceData, mainLight, _DirectLightMultiplier);

#if defined(_ADDITIONAL_LIGHTS)
    uint pixelLightCount = GetAdditionalLightsCount();
    half4 shadowMask = half4(1.0, 1.0, 1.0, 1.0);
    InputData inputData = (InputData)0;
    inputData.positionWS = positionWS;
    inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);

    #if USE_CLUSTER_LIGHT_LOOP
    [loop] for (uint lightIndex = 0; lightIndex < min(URP_FP_DIRECTIONAL_LIGHTS_COUNT, MAX_VISIBLE_LIGHTS); lightIndex++)
    {
        CLUSTER_LIGHT_LOOP_SUBTRACTIVE_LIGHT_CHECK
        Light light = GetAdditionalLight(lightIndex, positionWS, shadowMask);
        direct += PowerToonShadeSingleLight(normalWS, positionWS, surfaceData, light, _AdditionalLightMultiplier);
    }
    #endif

    LIGHT_LOOP_BEGIN(pixelLightCount)
        Light light = GetAdditionalLight(lightIndex, positionWS, shadowMask);
        direct += PowerToonShadeSingleLight(normalWS, positionWS, surfaceData, light, _AdditionalLightMultiplier);
    LIGHT_LOOP_END
#endif

    half3 lightResult = max(indirect, direct);
    return surfaceData.albedo * lightResult + surfaceData.emission;
}

half4 PowerToonFragment(PowerToonVaryings input) : SV_TARGET
{
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

    PowerToonSurfaceData surfaceData = PowerToonInitializeSurface(input);
    half3 normalWS = PowerToonGetNormalWS(input);
    half3 color = PowerToonShadeAllLights(input, surfaceData, normalWS);

#if defined(POWER_TOON_OUTLINE)
    color = _OutlineColor;
#endif

    color = MixFog(color, input.positionWSAndFog.w);
    return half4(color, surfaceData.alpha);
}

void PowerToonDepthFragment(PowerToonVaryings input)
{
    PowerToonAlphaClip(PowerToonSampleBase(input).a);

#if defined(LOD_FADE_CROSSFADE)
    LODFadeCrossFade(input.positionCS);
#endif
}

half PowerToonDepthOnlyFragment(PowerToonVaryings input) : SV_TARGET
{
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
    PowerToonDepthFragment(input);
    return input.positionCS.z;
}

void PowerToonDepthNormalsFragment(
    PowerToonVaryings input,
    out half4 outNormalWS : SV_Target0
#ifdef _WRITE_RENDERING_LAYERS
    , out float4 outRenderingLayers : SV_Target1
#endif
)
{
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
    PowerToonDepthFragment(input);

    half3 normalWS = PowerToonGetNormalWS(input);

#if defined(_GBUFFER_NORMALS_OCT)
    float2 octNormalWS = PackNormalOctQuadEncode(normalWS);
    float2 remappedOctNormalWS = saturate(octNormalWS * 0.5 + 0.5);
    half3 packedNormalWS = PackFloat2To888(remappedOctNormalWS);
    outNormalWS = half4(packedNormalWS, 0.0);
#else
    outNormalWS = half4(NormalizeNormalPerPixel(normalWS), 0.0);
#endif

#ifdef _WRITE_RENDERING_LAYERS
    uint renderingLayers = GetMeshRenderingLayer();
    outRenderingLayers = float4(EncodeMeshRenderingLayer(renderingLayers), 0, 0, 0);
#endif
}
