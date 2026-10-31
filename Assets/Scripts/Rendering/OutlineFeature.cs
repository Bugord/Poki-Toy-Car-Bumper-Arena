using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace ToyCarArena.Rendering
{
    /// <summary>
    /// Draws the comic-book outline: one extra pass after the opaque objects that redraws the renderers carrying
    /// <see cref="Outline.Layer"/> with the outline material (see <c>sh_outline</c>). Not post-processing, so there is no
    /// full-screen work, which matters on weak phones; the cost is one more draw per outlined renderer. The built-in Render
    /// Objects feature filters by physics layer only, hence this small feature that filters by rendering layer.
    /// </summary>
    public sealed class OutlineFeature : ScriptableRendererFeature
    {
        [Tooltip("The outline material; the shader ships in the build because the renderer asset references it")]
        [SerializeField]
        private Material material;

        private OutlinePass pass;

        public override void Create()
        {
            this.pass = new OutlinePass { renderPassEvent = RenderPassEvent.AfterRenderingOpaques };
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (this.material == null || renderingData.cameraData.cameraType != CameraType.Game) {
                return;
            }

            this.pass.Material = this.material;
            renderer.EnqueuePass(this.pass);
        }

        private sealed class OutlinePass : ScriptableRenderPass
        {
            public Material Material;

            // Instance fields, not static: a shader tag cannot be made in a static initialiser while the feature asset loads.
            private readonly ShaderTagId[] tags =
            {
                new("UniversalForward"), new("UniversalForwardOnly"), new("SRPDefaultUnlit"),
            };

            private sealed class PassData
            {
                public RendererListHandle List;
            }

            public OutlinePass()
            {
                this.profilingSampler = new ProfilingSampler("Outline");
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                var camera = frameData.Get<UniversalCameraData>();
                var rendering = frameData.Get<UniversalRenderingData>();
                var resources = frameData.Get<UniversalResourceData>();

                var sorting = new SortingSettings(camera.camera) { criteria = camera.defaultOpaqueSortFlags };
                var drawing = new DrawingSettings(this.tags[0], sorting)
                {
                    overrideMaterial = this.Material,
                    overrideMaterialPassIndex = 0,
                };
                for (var i = 1; i < this.tags.Length; i++) {
                    drawing.SetShaderPassName(i, this.tags[i]);
                }

                var filtering = new FilteringSettings(RenderQueueRange.opaque, -1, Outline.Layer);

                using var builder = renderGraph.AddRasterRenderPass<PassData>("Outline", out var data, this.profilingSampler);
                data.List = renderGraph.CreateRendererList(new RendererListParams(rendering.cullResults, drawing, filtering));
                builder.UseRendererList(data.List);
                builder.SetRenderAttachment(resources.activeColorTexture, 0);
                builder.SetRenderAttachmentDepth(resources.activeDepthTexture, AccessFlags.ReadWrite);
                builder.SetRenderFunc(static (PassData passData, RasterGraphContext context) =>
                {
                    context.cmd.DrawRendererList(passData.List);
                });
            }
        }
    }
}
