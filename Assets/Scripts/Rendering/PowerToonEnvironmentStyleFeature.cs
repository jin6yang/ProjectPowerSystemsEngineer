using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Rendering.Universal;

namespace ProjectProtocol.Rendering
{
    public sealed class PowerToonEnvironmentStyleFeature : ScriptableRendererFeature
    {
        [System.Serializable]
        public sealed class Settings
        {
            public bool enabled = true;
            public RenderPassEvent renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing;

            [Header("Lighting")]
            public Vector3 sunDirectionWS = new Vector3(-0.35f, 0.75f, -0.45f);
            [Range(0f, 1f)] public float lightingInfluence = 0.28f;
            [Range(-1f, 1f)] public float rampThreshold = 0.18f;
            [Range(0.001f, 0.5f)] public float rampSoftness = 0.08f;
            [Range(2f, 6f)] public float rampSteps = 3f;
            [Range(0f, 1f)] public float rampHardness = 0.55f;
            [Range(0f, 1f)] public float topFaceWarmth = 0.12f;

            [Header("Color")]
            public Color shadowTint = new Color(0.34f, 0.43f, 0.52f, 1f);
            public Color highlightTint = new Color(1.0f, 0.92f, 0.74f, 1f);
            [Range(0f, 1f)] public float shadowTintStrength = 0.22f;
            [Range(0f, 1f)] public float highlightTintStrength = 0.08f;
            [Range(0.5f, 1.5f)] public float saturation = 0.9f;
            [Range(0.5f, 1.5f)] public float contrast = 1.06f;
            [Range(0.5f, 1.5f)] public float exposure = 0.96f;
            [Range(2f, 16f)] public float posterizeSteps = 9f;
            [Range(0f, 1f)] public float posterizeStrength = 0.12f;

            [Header("Atmosphere")]
            public Color hazeColor = new Color(0.62f, 0.72f, 0.80f, 1f);
            [Range(0f, 1f)] public float hazeStrength = 0.1f;
            public float hazeStart = 18f;
            public float hazeEnd = 70f;
        }

        public Settings settings = new Settings();

        private static readonly int SunDirectionId = Shader.PropertyToID("_SunDirectionWS");
        private static readonly int LightingInfluenceId = Shader.PropertyToID("_LightingInfluence");
        private static readonly int RampThresholdId = Shader.PropertyToID("_RampThreshold");
        private static readonly int RampSoftnessId = Shader.PropertyToID("_RampSoftness");
        private static readonly int RampStepsId = Shader.PropertyToID("_RampSteps");
        private static readonly int RampHardnessId = Shader.PropertyToID("_RampHardness");
        private static readonly int TopFaceWarmthId = Shader.PropertyToID("_TopFaceWarmth");
        private static readonly int ShadowTintId = Shader.PropertyToID("_ShadowTint");
        private static readonly int HighlightTintId = Shader.PropertyToID("_HighlightTint");
        private static readonly int ShadowTintStrengthId = Shader.PropertyToID("_ShadowTintStrength");
        private static readonly int HighlightTintStrengthId = Shader.PropertyToID("_HighlightTintStrength");
        private static readonly int SaturationId = Shader.PropertyToID("_Saturation");
        private static readonly int ContrastId = Shader.PropertyToID("_Contrast");
        private static readonly int ExposureId = Shader.PropertyToID("_Exposure");
        private static readonly int PosterizeStepsId = Shader.PropertyToID("_PosterizeSteps");
        private static readonly int PosterizeStrengthId = Shader.PropertyToID("_PosterizeStrength");
        private static readonly int HazeColorId = Shader.PropertyToID("_HazeColor");
        private static readonly int HazeStrengthId = Shader.PropertyToID("_HazeStrength");
        private static readonly int HazeStartId = Shader.PropertyToID("_HazeStart");
        private static readonly int HazeEndId = Shader.PropertyToID("_HazeEnd");
        private static readonly int BlitTextureId = Shader.PropertyToID("_BlitTexture");
        private static readonly int BlitScaleBiasId = Shader.PropertyToID("_BlitScaleBias");

        private Material material;
        private EnvironmentStylePass pass;
        private static MaterialPropertyBlock sharedPropertyBlock;
        private static MaterialPropertyBlock SharedPropertyBlock => sharedPropertyBlock ??= new MaterialPropertyBlock();

        public override void Create()
        {
            Shader shader = Shader.Find("Hidden/PowerSystem/Toon/EnvironmentStyle");
            material = shader != null ? CoreUtils.CreateEngineMaterial(shader) : null;
            pass = new EnvironmentStylePass(settings)
            {
                renderPassEvent = settings.renderPassEvent,
                requiresIntermediateTexture = true
            };
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (!settings.enabled || material == null)
            {
                return;
            }

            CameraType cameraType = renderingData.cameraData.cameraType;
            if (cameraType == CameraType.Preview || cameraType == CameraType.Reflection || !renderingData.cameraData.postProcessEnabled)
            {
                return;
            }

            ApplyMaterialSettings();
            pass.renderPassEvent = settings.renderPassEvent;
            pass.Setup(material);
            renderer.EnqueuePass(pass);
        }

        protected override void Dispose(bool disposing)
        {
            CoreUtils.Destroy(material);
            pass?.Dispose();
        }

        private void ApplyMaterialSettings()
        {
            Vector3 sunDirection = settings.sunDirectionWS.sqrMagnitude > 0.001f ? settings.sunDirectionWS.normalized : Vector3.up;
            material.SetVector(SunDirectionId, new Vector4(sunDirection.x, sunDirection.y, sunDirection.z, 0f));
            material.SetFloat(LightingInfluenceId, settings.lightingInfluence);
            material.SetFloat(RampThresholdId, settings.rampThreshold);
            material.SetFloat(RampSoftnessId, settings.rampSoftness);
            material.SetFloat(RampStepsId, settings.rampSteps);
            material.SetFloat(RampHardnessId, settings.rampHardness);
            material.SetFloat(TopFaceWarmthId, settings.topFaceWarmth);
            material.SetColor(ShadowTintId, settings.shadowTint);
            material.SetColor(HighlightTintId, settings.highlightTint);
            material.SetFloat(ShadowTintStrengthId, settings.shadowTintStrength);
            material.SetFloat(HighlightTintStrengthId, settings.highlightTintStrength);
            material.SetFloat(SaturationId, settings.saturation);
            material.SetFloat(ContrastId, settings.contrast);
            material.SetFloat(ExposureId, settings.exposure);
            material.SetFloat(PosterizeStepsId, settings.posterizeSteps);
            material.SetFloat(PosterizeStrengthId, settings.posterizeStrength);
            material.SetColor(HazeColorId, settings.hazeColor);
            material.SetFloat(HazeStrengthId, settings.hazeStrength);
            material.SetFloat(HazeStartId, settings.hazeStart);
            material.SetFloat(HazeEndId, Mathf.Max(settings.hazeStart + 0.01f, settings.hazeEnd));
        }

        private sealed class EnvironmentStylePass : ScriptableRenderPass
        {
            private readonly Settings settings;
            private Material material;
            private RTHandle colorCopy;

            public EnvironmentStylePass(Settings settings)
            {
                this.settings = settings;
                profilingSampler = new ProfilingSampler(nameof(PowerToonEnvironmentStyleFeature));
                ConfigureInput(ScriptableRenderPassInput.Depth | ScriptableRenderPassInput.Normal);
            }

            public void Setup(Material material)
            {
                this.material = material;
            }

            public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
            {
                RenderTextureDescriptor descriptor = renderingData.cameraData.cameraTargetDescriptor;
                descriptor.depthBufferBits = 0;
                descriptor.msaaSamples = 1;

                RenderingUtils.ReAllocateHandleIfNeeded(
                    ref colorCopy,
                    descriptor,
                    FilterMode.Bilinear,
                    TextureWrapMode.Clamp,
                    name: "_PowerToonEnvironmentStyleColorCopy");
            }

            public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
            {
                if (!settings.enabled || material == null)
                {
                    return;
                }

                CommandBuffer cmd = CommandBufferPool.Get("Power Toon Environment Style");
                using (new ProfilingScope(cmd, profilingSampler))
                {
                    RTHandle cameraColor = renderingData.cameraData.renderer.cameraColorTargetHandle;
                    Blitter.BlitCameraTexture(cmd, cameraColor, colorCopy);
                    Blitter.BlitCameraTexture(cmd, colorCopy, cameraColor, material, 0);
                }

                context.ExecuteCommandBuffer(cmd);
                CommandBufferPool.Release(cmd);
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                if (!settings.enabled || material == null)
                {
                    return;
                }

                UniversalResourceData resources = frameData.Get<UniversalResourceData>();
                if (!resources.activeColorTexture.IsValid())
                {
                    return;
                }

                TextureHandle source = resources.activeColorTexture;
                TextureDesc copyDescriptor = renderGraph.GetTextureDesc(source);
                copyDescriptor.name = "_PowerToonEnvironmentStyleColorCopy";
                copyDescriptor.depthBufferBits = 0;
                copyDescriptor.clearBuffer = false;

                TextureHandle colorCopyHandle = renderGraph.CreateTexture(copyDescriptor);
                renderGraph.AddBlitPass(source, colorCopyHandle, Vector2.one, Vector2.zero, passName: "Copy Color For Power Toon Environment Style");

                using (IRasterRenderGraphBuilder builder = renderGraph.AddRasterRenderPass<PassData>("Power Toon Environment Style", out PassData passData, profilingSampler))
                {
                    passData.material = material;
                    passData.inputTexture = colorCopyHandle;

                    builder.UseTexture(passData.inputTexture, AccessFlags.Read);

                    if (resources.cameraDepthTexture.IsValid())
                    {
                        builder.UseTexture(resources.cameraDepthTexture, AccessFlags.Read);
                    }

                    if (resources.cameraNormalsTexture.IsValid())
                    {
                        builder.UseTexture(resources.cameraNormalsTexture, AccessFlags.Read);
                    }

                    builder.SetRenderAttachment(resources.activeColorTexture, 0, AccessFlags.Write);
                    builder.SetRenderFunc((PassData data, RasterGraphContext context) =>
                    {
                        DrawFullscreen(context.cmd, data.inputTexture, data.material);
                    });
                }
            }

            public void Dispose()
            {
                colorCopy?.Release();
            }

            private static void DrawFullscreen(RasterCommandBuffer cmd, RTHandle sourceTexture, Material material)
            {
                SharedPropertyBlock.Clear();
                SharedPropertyBlock.SetTexture(BlitTextureId, sourceTexture);
                SharedPropertyBlock.SetVector(BlitScaleBiasId, new Vector4(1f, 1f, 0f, 0f));
                cmd.DrawProcedural(Matrix4x4.identity, material, 0, MeshTopology.Triangles, 3, 1, SharedPropertyBlock);
            }

            private sealed class PassData
            {
                public Material material;
                public TextureHandle inputTexture;
            }
        }
    }
}
