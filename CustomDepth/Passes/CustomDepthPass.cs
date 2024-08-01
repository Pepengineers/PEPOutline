using System.Collections.Generic;
using Pepengineers.PEPOutline.CustomDepth.Data;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace Pepengineers.PEPOutline.CustomDepth.Passes
{
    internal sealed class CustomDepthPass : ScriptableRenderPass
    {
        private const string CustomDepthTextureName = "_CustomDepthTexture";
        private static readonly int CustomDepthTextureId = Shader.PropertyToID(CustomDepthTextureName);


        private static readonly List<ShaderTagId> ShaderTags
            = new()
            {
                new ShaderTagId("DepthOnly")
            };


        private readonly Shader customShader;
        private readonly FilteringSettings filteringSettings;
        private RenderTextureDescriptor customDepthDesc;


        public CustomDepthPass(RenderPassEvent evt, DepthFormat format, LayerMask layerMask, RenderingLayerMask renderingLayerMask, RenderQueueRange range, Shader customShader)
        {
            this.customShader = customShader;
            profilingSampler = new ProfilingSampler("CustomDepth");
            renderPassEvent = evt;
            filteringSettings = new FilteringSettings(range, layerMask, renderingLayerMask);
            customDepthDesc = new RenderTextureDescriptor(Screen.width, Screen.height, GraphicsFormat.None, (GraphicsFormat)format)
            {
                width = Screen.width,
                height = Screen.height,
                useMipMap = false,
                autoGenerateMips = false,
                bindMS = false
            };
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            var customDepthData = frameData.GetOrCreate<CustomDepthData>();
            var renderingData = frameData.Get<UniversalRenderingData>();
            var resourceData = frameData.Get<UniversalResourceData>();
            var cameraData = frameData.Get<UniversalCameraData>();
            var lightData = frameData.Get<UniversalLightData>();

            using var builder = renderGraph.AddRasterRenderPass<PassData>(string.Empty, out var passData, profilingSampler);

            builder.UseAllGlobalTextures(true);

            var currentDepthDesc = cameraData.cameraTargetDescriptor;
            customDepthDesc.width = currentDepthDesc.width;
            customDepthDesc.height = currentDepthDesc.height;
            customDepthData.CustomDepth = UniversalRenderer.CreateRenderGraphTexture(renderGraph, customDepthDesc, CustomDepthTextureName, true);
            builder.SetRenderAttachmentDepth(customDepthData.CustomDepth);

            var sortFlags = cameraData.defaultOpaqueSortFlags;
            if (cameraData.renderType == CameraRenderType.Base || cameraData.clearDepth)
                sortFlags = SortingCriteria.SortingLayer | SortingCriteria.RenderQueue | SortingCriteria.OptimizeStateChanges | SortingCriteria.CanvasOrder;


            var drawSettings = RenderingUtils.CreateDrawingSettings(ShaderTags, renderingData, cameraData, lightData, sortFlags);

            if (customShader != null)
            {
                drawSettings.overrideMaterial = null;
                drawSettings.overrideMaterialPassIndex = 0;
                drawSettings.overrideShader = customShader;
                drawSettings.overrideShaderPassIndex = 0;
            }

            passData.RendererList = renderGraph.CreateRendererList(new RendererListParams(renderingData.cullResults, drawSettings, filteringSettings) { isPassTagName = false });

            builder.UseRendererList(passData.RendererList);
            builder.AllowPassCulling(false);
            builder.AllowGlobalStateModification(true);
            builder.SetGlobalTextureAfterPass(customDepthData.CustomDepth, CustomDepthTextureId);

            builder.SetRenderFunc((PassData data, RasterGraphContext context) =>
            {
                var cmd = context.cmd;

                using (new ProfilingScope(cmd, profilingSampler))
                {
                    cmd.DrawRendererList(data.RendererList);
                }
            });
        }


        private class PassData
        {
            internal RendererListHandle RendererList;
        }
    }
}