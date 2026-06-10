using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.RenderGraphModule;
namespace KillingMahjong.UISetUp
{
    public class CRTRenderFeature : ScriptableRendererFeature
    {
        [System.Serializable]
        public class CRTSettings
        {
            [Tooltip("テスト用にエフェクトのON/OFFを切り替えます")]
            public bool isEnabled = true;
            public Material crtMaterial = null;
            public RenderPassEvent renderPassEvent = RenderPassEvent.AfterRenderingPostProcessing;
        }

        public CRTSettings settings = new CRTSettings();
        private CRTRenderPass m_RenderPass;

        public override void Create()
        {
            if (settings.crtMaterial == null) return;
            
            m_RenderPass = new CRTRenderPass(settings.crtMaterial);
            m_RenderPass.renderPassEvent = settings.renderPassEvent;
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (!settings.isEnabled || settings.crtMaterial == null) return;
                
            // Apply only to game/scene cameras, depending on preference
            if (renderingData.cameraData.cameraType == CameraType.Game || renderingData.cameraData.cameraType == CameraType.SceneView)
            {
                renderer.EnqueuePass(m_RenderPass);
            }
        }
        
        protected override void Dispose(bool disposing)
        {
            if (m_RenderPass != null)
                m_RenderPass.Dispose();
        }

        class CRTRenderPass : ScriptableRenderPass
        {
            private Material m_Material;
            private RTHandle m_TempColorTexture;

            public CRTRenderPass(Material material)
            {
                m_Material = material;
            }

#pragma warning disable CS0672, CS0618
            public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
            {
                // Retrieve the camera color target
                var colorDesc = renderingData.cameraData.cameraTargetDescriptor;
                colorDesc.depthBufferBits = 0; // We don't need depth for the temp texture
                
                // Allocate temporary RTHandle (URP 14+ method)
                RenderingUtils.ReAllocateIfNeeded(ref m_TempColorTexture, colorDesc, FilterMode.Bilinear, TextureWrapMode.Clamp, name: "_TempCRTColorTex");
            }
#pragma warning restore CS0672, CS0618

#pragma warning disable CS0672, CS0618
            public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
            {
                if (m_Material == null) return;

                CommandBuffer cmd = CommandBufferPool.Get("Execute CRT Filter");

                // Grab the camera's original color target
                var cameraColorTarget = renderingData.cameraData.renderer.cameraColorTargetHandle;

                // Blitter API is recommended in URP 14+
                // 1. Blit from camera target to temp texture using our material
                Blitter.BlitCameraTexture(cmd, cameraColorTarget, m_TempColorTexture, m_Material, 0);
                
                // 2. Blit back to camera target
                Blitter.BlitCameraTexture(cmd, m_TempColorTexture, cameraColorTarget);

                context.ExecuteCommandBuffer(cmd);
                CommandBufferPool.Release(cmd);
            }

            private class PassData
            {
                public TextureHandle sourceTexture;
                public TextureHandle tempTexture;
                public Material material;
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                if (m_Material == null) return;

                var resourceData = frameData.Get<UniversalResourceData>();
                var sourceTexture = resourceData.activeColorTexture;
                if (!sourceTexture.IsValid()) return;

                var cameraData = frameData.Get<UniversalCameraData>();
                var desc = cameraData.cameraTargetDescriptor;
                desc.depthBufferBits = 0;

                TextureHandle tempTexture = UniversalRenderer.CreateRenderGraphTexture(renderGraph, desc, "_TempCRTColorTex", false);

                using (var builder = renderGraph.AddRasterRenderPass<PassData>("CRT Filter Blit Pass", out var passData))
                {
                    passData.material = m_Material;
                    passData.sourceTexture = sourceTexture;
                    passData.tempTexture = tempTexture;

                    builder.UseTexture(sourceTexture, AccessFlags.Read);
                    builder.SetRenderAttachment(tempTexture, 0);

                    builder.SetRenderFunc((PassData data, RasterGraphContext context) =>
                    {
                        Blitter.BlitTexture(context.cmd, data.sourceTexture, new Vector4(1, 1, 0, 0), data.material, 0);
                    });
                }

                using (var builder = renderGraph.AddRasterRenderPass<PassData>("CRT Filter Blit Back", out var passData))
                {
                    passData.tempTexture = tempTexture;
                    passData.sourceTexture = sourceTexture;

                    builder.UseTexture(tempTexture, AccessFlags.Read);
                    builder.SetRenderAttachment(sourceTexture, 0);

                    builder.SetRenderFunc((PassData data, RasterGraphContext context) =>
                    {
                        Blitter.BlitTexture(context.cmd, data.tempTexture, new Vector4(1, 1, 0, 0), 0.0f, false);
                    });
                }
            }

            public override void OnCameraCleanup(CommandBuffer cmd)
            {
            }
            
            public void Dispose()
            {
                if (m_TempColorTexture != null)
                    m_TempColorTexture.Release();
            }
        }
    }
}
