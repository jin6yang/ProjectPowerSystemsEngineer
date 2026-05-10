Shader "Hidden/PowerSystem/Toon/ScreenSpaceOutline"
{
    Properties
    {
        _OutlineColor("Outline Color", Color) = (0.055, 0.07, 0.085, 1)
        _Intensity("Intensity", Range(0, 1)) = 0.38
        _Thickness("Thickness", Range(0.5, 4)) = 1.15
        _DepthThreshold("Depth Threshold", Range(0.0001, 0.03)) = 0.0035
        _DepthStrength("Depth Strength", Range(0, 4)) = 1
        _NormalThreshold("Normal Threshold", Range(0.01, 1)) = 0.22
        _NormalStrength("Normal Strength", Range(0, 4)) = 0.75
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
            Name "ScreenSpaceOutline"

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareNormalsTexture.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _OutlineColor;
                half _Intensity;
                half _Thickness;
                half _DepthThreshold;
                half _DepthStrength;
                half _NormalThreshold;
                half _NormalStrength;
            CBUFFER_END

            float SampleLinearEyeDepth(float2 uv)
            {
                float rawDepth = SampleSceneDepth(uv);
                return LinearEyeDepth(rawDepth, _ZBufferParams);
            }

            half GetDepthEdge(float2 uv, float2 texel)
            {
                float centerDepth = SampleLinearEyeDepth(uv);
                float leftDepth = SampleLinearEyeDepth(uv - float2(texel.x, 0.0));
                float rightDepth = SampleLinearEyeDepth(uv + float2(texel.x, 0.0));
                float downDepth = SampleLinearEyeDepth(uv - float2(0.0, texel.y));
                float upDepth = SampleLinearEyeDepth(uv + float2(0.0, texel.y));

                float depthDelta = 0.0;
                depthDelta = max(depthDelta, abs(centerDepth - leftDepth));
                depthDelta = max(depthDelta, abs(centerDepth - rightDepth));
                depthDelta = max(depthDelta, abs(centerDepth - downDepth));
                depthDelta = max(depthDelta, abs(centerDepth - upDepth));
                depthDelta /= max(centerDepth, 0.001);

                return smoothstep(_DepthThreshold, _DepthThreshold * 2.0, depthDelta) * _DepthStrength;
            }

            half GetNormalEdge(float2 uv, float2 texel)
            {
                half3 centerNormal = normalize(SampleSceneNormals(uv));
                half3 leftNormal = normalize(SampleSceneNormals(uv - float2(texel.x, 0.0)));
                half3 rightNormal = normalize(SampleSceneNormals(uv + float2(texel.x, 0.0)));
                half3 downNormal = normalize(SampleSceneNormals(uv - float2(0.0, texel.y)));
                half3 upNormal = normalize(SampleSceneNormals(uv + float2(0.0, texel.y)));

                half normalDelta = 0.0;
                normalDelta = max(normalDelta, distance(centerNormal, leftNormal));
                normalDelta = max(normalDelta, distance(centerNormal, rightNormal));
                normalDelta = max(normalDelta, distance(centerNormal, downNormal));
                normalDelta = max(normalDelta, distance(centerNormal, upNormal));

                return smoothstep(_NormalThreshold, _NormalThreshold * 1.5, normalDelta) * _NormalStrength;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 uv = input.texcoord;
                half4 color = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_PointClamp, uv);
                float2 texel = _CameraDepthTexture_TexelSize.xy * _Thickness;

                half depthEdge = GetDepthEdge(uv, texel);
                half normalEdge = GetNormalEdge(uv, texel);
                half edge = saturate(max(depthEdge, normalEdge) * _Intensity);

                color.rgb = lerp(color.rgb, _OutlineColor.rgb, edge * _OutlineColor.a);
                return color;
            }
            ENDHLSL
        }
    }
}
