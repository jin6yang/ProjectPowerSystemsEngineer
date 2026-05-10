Shader "Hidden/PowerSystem/Toon/EnvironmentStyle"
{
    Properties
    {
        _SunDirectionWS("Sun Direction WS", Vector) = (-0.35, 0.75, -0.45, 0)
        _LightingInfluence("Lighting Influence", Range(0, 1)) = 0.28
        _RampThreshold("Ramp Threshold", Range(-1, 1)) = 0.18
        _RampSoftness("Ramp Softness", Range(0.001, 0.5)) = 0.08
        _RampSteps("Ramp Steps", Range(2, 6)) = 3
        _RampHardness("Ramp Hardness", Range(0, 1)) = 0.55
        _TopFaceWarmth("Top Face Warmth", Range(0, 1)) = 0.12
        _ShadowTint("Shadow Tint", Color) = (0.34, 0.43, 0.52, 1)
        _HighlightTint("Highlight Tint", Color) = (1.0, 0.92, 0.74, 1)
        _ShadowTintStrength("Shadow Tint Strength", Range(0, 1)) = 0.22
        _HighlightTintStrength("Highlight Tint Strength", Range(0, 1)) = 0.08
        _Saturation("Saturation", Range(0.5, 1.5)) = 0.9
        _Contrast("Contrast", Range(0.5, 1.5)) = 1.06
        _Exposure("Exposure", Range(0.5, 1.5)) = 0.96
        _PosterizeSteps("Posterize Steps", Range(2, 16)) = 9
        _PosterizeStrength("Posterize Strength", Range(0, 1)) = 0.12
        _HazeColor("Haze Color", Color) = (0.62, 0.72, 0.80, 1)
        _HazeStrength("Haze Strength", Range(0, 1)) = 0.1
        _HazeStart("Haze Start", Float) = 18
        _HazeEnd("Haze End", Float) = 70
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Opaque"
        }

        ZWrite Off
        ZTest Always
        Cull Off

        Pass
        {
            Name "EnvironmentStyle"

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareNormalsTexture.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half3 _SunDirectionWS;
                half _LightingInfluence;
                half _RampThreshold;
                half _RampSoftness;
                half _RampSteps;
                half _RampHardness;
                half _TopFaceWarmth;
                half3 _ShadowTint;
                half3 _HighlightTint;
                half _ShadowTintStrength;
                half _HighlightTintStrength;
                half _Saturation;
                half _Contrast;
                half _Exposure;
                half _PosterizeSteps;
                half _PosterizeStrength;
                half3 _HazeColor;
                half _HazeStrength;
                float _HazeStart;
                float _HazeEnd;
            CBUFFER_END

            float SampleLinearEyeDepth(float2 uv)
            {
                float rawDepth = SampleSceneDepth(uv);
                return LinearEyeDepth(rawDepth, _ZBufferParams);
            }

            half QuantizeRamp(half value)
            {
                half steps = max(2.0, _RampSteps);
                half quantized = floor(saturate(value) * (steps - 0.0001)) / max(1.0, steps - 1.0);
                return lerp(value, quantized, _RampHardness);
            }

            half3 ApplyColorGrade(half3 color)
            {
                color *= _Exposure;

                half luminance = dot(color, half3(0.2126, 0.7152, 0.0722));
                color = lerp(luminance.xxx, color, _Saturation);
                color = (color - 0.5) * _Contrast + 0.5;

                half3 posterized = floor(saturate(color) * _PosterizeSteps + 0.5) / _PosterizeSteps;
                color = lerp(color, posterized, _PosterizeStrength);
                return max(color, 0.0);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 uv = input.texcoord;
                half3 color = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_PointClamp, uv).rgb;
                half3 normalWS = normalize(SampleSceneNormals(uv));

                half sunAmount = dot(normalWS, normalize(_SunDirectionWS));
                half ramp = smoothstep(_RampThreshold - _RampSoftness, _RampThreshold + _RampSoftness, sunAmount);
                ramp = QuantizeRamp(ramp);

                half topMask = saturate(normalWS.y);
                half shadowMask = 1.0 - ramp;

                half3 stylized = color;
                stylized = lerp(stylized, stylized * _ShadowTint, shadowMask * _ShadowTintStrength * _LightingInfluence);
                stylized = lerp(stylized, stylized * _HighlightTint, ramp * _HighlightTintStrength * _LightingInfluence);
                stylized += _HighlightTint * topMask * _TopFaceWarmth * _LightingInfluence;

                color = lerp(color, stylized, _LightingInfluence);
                color = ApplyColorGrade(color);

                float depth = SampleLinearEyeDepth(uv);
                half haze = smoothstep(_HazeStart, _HazeEnd, depth) * _HazeStrength;
                color = lerp(color, _HazeColor, haze);

                return half4(color, 1.0);
            }
            ENDHLSL
        }
    }
}
