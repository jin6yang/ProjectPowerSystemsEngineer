Shader "PowerSystem/Toon/GhostTransparent"
{
    Properties
    {
        [Header(Base)]
        [MainTexture] _BaseMap("Base Map", 2D) = "white" {}
        [HDR][MainColor] _BaseColor("Base Color", Color) = (1, 1, 1, 0.45)
        _Alpha("Alpha Multiplier", Range(0, 1)) = 1
        _MinimumAlpha("Minimum Alpha", Range(0, 1)) = 0.22
        _TopFaceAlphaBoost("Top Face Alpha Boost", Range(0, 1)) = 0.28

        [Header(Depth)]
        _DepthOffsetFactor("Depth Offset Factor", Float) = -1
        _DepthOffsetUnits("Depth Offset Units", Float) = -1

        [Header(Toon Lighting)]
        _CelShadeMidPoint("Shadow Threshold", Range(-1, 1)) = -0.2
        _CelShadeSoftness("Shadow Softness", Range(0.001, 0.5)) = 0.08
        _AmbientMinColor("Ambient Minimum", Color) = (0.18, 0.21, 0.24, 1)
        _DirectLightMultiplier("Main Light Multiplier", Range(0, 2)) = 0.75
        _ShadowColor("Toon Shadow Color", Color) = (0.26, 0.34, 0.42, 1)
        _ShadowStrength("Toon Shadow Strength", Range(0, 1)) = 0.45

        [Header(Ghost Edge)]
        [HDR] _RimColor("Rim Color", Color) = (0.45, 0.8, 1.0, 1)
        _RimPower("Rim Power", Range(0.5, 8)) = 2.2
        _RimStrength("Rim Strength", Range(0, 4)) = 0.8
        _RimAlphaStrength("Rim Alpha Strength", Range(0, 1)) = 0.28
        _PulseStrength("Pulse Strength", Range(0, 1)) = 0.08
        _PulseSpeed("Pulse Speed", Range(0, 8)) = 2

        [Header(Emission)]
        [HDR] _EmissionColor("Emission Color", Color) = (0.2, 0.55, 1, 1)
        _EmissionStrength("Emission Strength", Range(0, 4)) = 0.15
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "UniversalMaterialType" = "Unlit"
        }

        LOD 100

        Pass
        {
            Name "GhostForward"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Off
            Offset [_DepthOffsetFactor], [_DepthOffsetUnits]

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex GhostVertex
            #pragma fragment GhostFragment

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ EVALUATE_SH_MIXED EVALUATE_SH_VERTEX
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Fog.hlsl"

            #pragma multi_compile_instancing
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
                half _Alpha;
                half _MinimumAlpha;
                half _TopFaceAlphaBoost;
                half _CelShadeMidPoint;
                half _CelShadeSoftness;
                half3 _AmbientMinColor;
                half _DirectLightMultiplier;
                half3 _ShadowColor;
                half _ShadowStrength;
                half3 _RimColor;
                half _RimPower;
                half _RimStrength;
                half _RimAlphaStrength;
                half _PulseStrength;
                half _PulseSpeed;
                half3 _EmissionColor;
                half _EmissionStrength;
            CBUFFER_END

            struct GhostAttributes
            {
                float3 positionOS : POSITION;
                half3 normalOS : NORMAL;
                half4 tangentOS : TANGENT;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct GhostVaryings
            {
                float2 uv : TEXCOORD0;
                float4 positionWSAndFog : TEXCOORD1;
                half3 normalWS : TEXCOORD2;
                float4 positionCS : SV_POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            GhostVaryings GhostVertex(GhostAttributes input)
            {
                GhostVaryings output;

                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                VertexPositionInputs positionInput = GetVertexPositionInputs(input.positionOS);
                VertexNormalInputs normalInput = GetVertexNormalInputs(input.normalOS, input.tangentOS);

                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                output.normalWS = normalInput.normalWS;
                output.positionCS = positionInput.positionCS;
                output.positionWSAndFog = float4(positionInput.positionWS, ComputeFogFactor(positionInput.positionCS.z));

                return output;
            }

            half4 GhostFragment(GhostVaryings input) : SV_TARGET
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                half4 baseSample = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv) * _BaseColor;
                half3 normalWS = normalize(input.normalWS);
                half3 viewDirectionWS = SafeNormalize(GetCameraPositionWS() - input.positionWSAndFog.xyz);
                half topFaceMask = saturate(normalWS.y);

                float4 shadowCoord = TransformWorldToShadowCoord(input.positionWSAndFog.xyz);
                Light mainLight = GetMainLight(shadowCoord);

                half noL = dot(normalWS, mainLight.direction);
                half lightAmount = smoothstep(_CelShadeMidPoint - _CelShadeSoftness, _CelShadeMidPoint + _CelShadeSoftness, noL);
                lightAmount *= mainLight.shadowAttenuation;

                half3 ambient = max(SampleSH(normalWS), _AmbientMinColor);
                half3 shadowTint = lerp(1.0, _ShadowColor, _ShadowStrength);
                half3 toonTone = lerp(shadowTint, 1.0, lightAmount);
                half3 litColor = baseSample.rgb * max(ambient, saturate(mainLight.color) * toonTone * _DirectLightMultiplier);

                half rim = pow(1.0 - saturate(dot(normalWS, viewDirectionWS)), _RimPower);
                half pulse = 1.0 + sin(_Time.y * _PulseSpeed) * _PulseStrength;
                half3 emission = (_RimColor * rim * _RimStrength + _EmissionColor * _EmissionStrength) * pulse;

                half alpha = max(baseSample.a * _Alpha, _MinimumAlpha);
                alpha = saturate(alpha + topFaceMask * _TopFaceAlphaBoost + rim * _RimAlphaStrength);
                half3 color = litColor + emission;
                color = MixFog(color, input.positionWSAndFog.w);

                return half4(color, alpha);
            }
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
