using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Rendering.Universal;

namespace ProjectProtocol.Rendering
{
    public sealed class PowerToonScreenSpaceOutlineFeature : ScriptableRendererFeature
    {
        [System.Serializable]
        public sealed class Settings
        {
            public bool enabled = true;
            public RenderPassEvent renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing;
            public Color outlineColor = new Color(0.055f, 0.07f, 0.085f, 1f);
            [Range(0f, 1f)] public float intensity = 0.38f;
            [Range(0.5f, 4f)] public float thickness = 1.15f;
            [Range(0.0001f, 0.3f)] public float depthThreshold = 0.0035f;
            [Range(0f, 4f)] public float depthStrength = 1.0f;
            [Range(0.01f, 4f)] public float normalThreshold = 0.22f;
            [Range(0f, 4f)] public float normalStrength = 0.75f;
        }

        public Settings settings = new Settings();

        private static readonly int OutlineColorId = Shader.PropertyToID("_OutlineColor");
        private static readonly int IntensityId = Shader.PropertyToID("_Intensity");
        private static readonly int ThicknessId = Shader.PropertyToID("_Thickness");
        private static readonly int DepthThresholdId = Shader.PropertyToID("_DepthThreshold");
        private static readonly int DepthStrengthId = Shader.PropertyToID("_DepthStrength");
        private static readonly int NormalThresholdId = Shader.PropertyToID("_NormalThreshold");
        private static readonly int NormalStrengthId = Shader.PropertyToID("_NormalStrength");
        private static readonly int BlitTextureId = Shader.PropertyToID("_BlitTexture");
        private static readonly int BlitScaleBiasId = Shader.PropertyToID("_BlitScaleBias");

        private Material material;
        private OutlinePass outlinePass;
        private static MaterialPropertyBlock sharedPropertyBlock;
        private static MaterialPropertyBlock SharedPropertyBlock => sharedPropertyBlock ??= new MaterialPropertyBlock();

        public override void Create()
        {
            Shader shader = Shader.Find("Hidden/PowerSystem/Toon/ScreenSpaceOutline");
            material = shader != null ? CoreUtils.CreateEngineMaterial(shader) : null;

            outlinePass = new OutlinePass(settings)
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

            material.SetColor(OutlineColorId, settings.outlineColor);
            material.SetFloat(IntensityId, settings.intensity);
            material.SetFloat(ThicknessId, settings.thickness);
            material.SetFloat(DepthThresholdId, settings.depthThreshold);
            material.SetFloat(DepthStrengthId, settings.depthStrength);
            material.SetFloat(NormalThresholdId, settings.normalThreshold);
            material.SetFloat(NormalStrengthId, settings.normalStrength);

            outlinePass.renderPassEvent = settings.renderPassEvent;
            outlinePass.Setup(material);
            renderer.EnqueuePass(outlinePass);
        }

        protected override void Dispose(bool disposing)
        {
            CoreUtils.Destroy(material);
            outlinePass?.Dispose();
        }

        private sealed class OutlinePass : ScriptableRenderPass
        {
            private readonly Settings settings;
            private Material material;
            private RTHandle colorCopy;

            public OutlinePass(Settings settings)
            {
                this.settings = settings;
                profilingSampler = new ProfilingSampler(nameof(PowerToonScreenSpaceOutlineFeature));
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
                    name: "_PowerToonScreenSpaceOutlineColorCopy");
            }

            public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
            {
                if (!settings.enabled || material == null)
                {
                    return;
                }

                CommandBuffer cmd = CommandBufferPool.Get("Power Toon Screen Space Outline");

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
                copyDescriptor.name = "_PowerToonScreenSpaceOutlineColorCopy";
                copyDescriptor.depthBufferBits = 0;
                copyDescriptor.clearBuffer = false;

                TextureHandle colorCopyHandle = renderGraph.CreateTexture(copyDescriptor);
                renderGraph.AddBlitPass(source, colorCopyHandle, Vector2.one, Vector2.zero, passName: "Copy Color For Power Toon Outline");

                using (IRasterRenderGraphBuilder builder = renderGraph.AddRasterRenderPass<PassData>("Power Toon Screen Space Outline", out PassData passData, profilingSampler))
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
