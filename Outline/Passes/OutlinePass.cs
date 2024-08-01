using System.Runtime.CompilerServices;
using Pepengineers.PEPOutline.CustomDepth.Data;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace Pepengineers.PEPOutline.Outline.Passes
{
    internal sealed class OutlinePass : ScriptableRenderPass
    {
        private const int TileSize = 8;
        private static readonly int ResultTextureID = Shader.PropertyToID("_ResultTexture");
        private static readonly int SceneDepthID = Shader.PropertyToID("_DepthTexture");
        private static readonly int MaskId = Shader.PropertyToID("_Mask");
        private static readonly int StencilId = Shader.PropertyToID("_Stencil");
        private static readonly int RadiusId = Shader.PropertyToID("_RadiusInPixels");
        private static readonly int TileSizeId = Shader.PropertyToID("_TileSize");

        private static readonly int EnemyColorID = Shader.PropertyToID("_EnemyColor");
        private static readonly int FriendColorID = Shader.PropertyToID("_FriendColor");
        private static readonly int NpcColorID = Shader.PropertyToID("_NeutralColor");
        private static readonly int InteractionColorID = Shader.PropertyToID("_InteractionColor");

        private readonly Material blurMaterial;
        private readonly Material composeMaterial;
        private readonly ComputeShader cutoutShader;


        public OutlinePass(ComputeShader computeShader, Material blur, Material compose)
        {
            renderPassEvent = RenderPassEvent.AfterRenderingPostProcessing;
            cutoutShader = computeShader;
            blurMaterial = blur;
            composeMaterial = compose;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int DivideAndRoundUp(int dividend, int divisor)
        {
            return (dividend + divisor - 1) / divisor;
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            var resourceData = frameData.Get<UniversalResourceData>();
            var cameraData = frameData.Get<UniversalCameraData>();
            var postProcessingData = frameData.Get<UniversalPostProcessingData>();
            var customDepthData = frameData.Get<CustomDepthData>();

            if (postProcessingData.isEnabled == false) return;
            if (cameraData.postProcessEnabled == false) return;
            if (cameraData.cameraType != CameraType.Game && cameraData.cameraType != CameraType.SceneView) return;
            if (cameraData.renderType != CameraRenderType.Base) return;

            var stack = VolumeManager.instance.stack;
            var outline = stack.GetComponent<Components.Outline>();
            if (outline == null || outline.IsActive() == false) return;

            var sceneColor = resourceData.activeColorTexture;

            var desc = cameraData.cameraTargetDescriptor;
            desc.depthStencilFormat = GraphicsFormat.None;
            desc.graphicsFormat = GraphicsFormat.R16G16B16A16_SFloat;

            var resultTexture = renderGraph.CreateTexture(new TextureDesc(desc)
            {
                name = "OutlineResult",
                enableRandomWrite = true
            });

            var alphaResultTexture = renderGraph.CreateTexture(new TextureDesc(desc)
            {
                name = "OutlineBlurredResult"
            });

            var tileCount =
                new int3(
                    DivideAndRoundUp(desc.width, TileSize - 1),
                    DivideAndRoundUp(desc.height, TileSize - 1),
                    1);
            var maskTexture = renderGraph.CreateTexture(new TextureDesc(tileCount.x, tileCount.y)
            {
                name = "OutlineMask",
                colorFormat = GraphicsFormat.R8_UInt,
                clearColor = Color.black,
                clearBuffer = true,
                enableRandomWrite = true
            });


            var sceneDepth = resourceData.activeDepthTexture;
            var customDepth = customDepthData.CustomDepth;

            if (sceneColor.IsValid() == false || sceneDepth.IsValid() == false ||
                resultTexture.IsValid() == false || maskTexture.IsValid() == false ||
                alphaResultTexture.IsValid() == false || customDepth.IsValid() == false)
                return;


            using (var builder = renderGraph.AddComputePass<CutoutData>("Cutout Outline", out var passData))
            {
                passData.TileCount = tileCount;
                passData.ComputeShader = cutoutShader;
                passData.StencilTexture = customDepth;
                passData.SceneDepthTexture = sceneDepth;
                passData.MaskTexture = maskTexture;
                passData.ResultTexture = resultTexture;

                builder.UseTexture(sceneDepth);
                builder.UseTexture(customDepth);
                builder.UseTexture(maskTexture, AccessFlags.Write);
                builder.UseTexture(resultTexture, AccessFlags.Write);

                builder.SetRenderFunc((CutoutData data, ComputeGraphContext context) =>
                {
                    var kernel = data.ComputeShader.FindKernel("CutoutPassCS");
                    context.cmd.SetComputeTextureParam(data.ComputeShader, kernel, ResultTextureID, data.ResultTexture);
                    context.cmd.SetComputeTextureParam(data.ComputeShader, kernel, MaskId, data.MaskTexture);
                    context.cmd.SetComputeTextureParam(data.ComputeShader, kernel, StencilId, data.StencilTexture, 0, RenderTextureSubElement.Stencil);
                    context.cmd.SetComputeTextureParam(data.ComputeShader, kernel, SceneDepthID, data.SceneDepthTexture, 0, RenderTextureSubElement.Depth);
                    context.cmd.DispatchCompute(data.ComputeShader, kernel, data.TileCount.x, data.TileCount.y, data.TileCount.z);
                });
            }


            using (var builder = renderGraph.AddRasterRenderPass<BlurData>("Outline Blur", out var blurData))
            {
                blurData.MaskTexture = maskTexture;
                blurData.ResultTexture = resultTexture;
                blurData.RadiusInPixels = outline.RadiusInPixels.value;

                builder.UseTexture(maskTexture);
                builder.UseTexture(resultTexture);
                builder.SetRenderAttachment(alphaResultTexture, 0, AccessFlags.WriteAll);

                builder.SetRenderFunc((BlurData data, RasterGraphContext context) =>
                {
                    blurMaterial.SetTexture(ResultTextureID, data.ResultTexture);
                    blurMaterial.SetTexture(MaskId, data.MaskTexture);
                    blurMaterial.SetFloat(RadiusId, data.RadiusInPixels);
                    blurMaterial.SetInt(TileSizeId, TileSize);
                    context.cmd.ClearRenderTarget(true, true, Color.black);
                    context.cmd.DrawProcedural(Matrix4x4.identity, blurMaterial, 0, MeshTopology.Triangles, 3);
                });
            }

            using (var builder = renderGraph.AddRasterRenderPass<ComposeData>("Outline Compose", out var composeData))
            {
                composeData.SceneDepthTexture = sceneDepth;
                composeData.EnemyColor = outline.EnemyColor.value;
                composeData.FriendColor = outline.FriendColor.value;
                composeData.NeutralColor = outline.NeutralColor.value;
                composeData.InteractionColor = outline.InteractionColor.value;
                composeData.RadiusInPixels = outline.RadiusInPixels.value;
                composeData.ResultTexture = alphaResultTexture;

                builder.UseTexture(sceneDepth);
                builder.UseTexture(alphaResultTexture);

                builder.SetRenderAttachment(sceneColor, 0);

                builder.SetRenderFunc((ComposeData data, RasterGraphContext context) =>
                {
                    composeMaterial.SetTexture(ResultTextureID, data.ResultTexture);
                    composeMaterial.SetTexture(SceneDepthID, data.SceneDepthTexture);
                    composeMaterial.SetFloat(RadiusId, data.RadiusInPixels);

                    composeMaterial.SetColor(EnemyColorID, data.EnemyColor);
                    composeMaterial.SetColor(FriendColorID, data.FriendColor);
                    composeMaterial.SetColor(NpcColorID, data.NeutralColor);
                    composeMaterial.SetColor(InteractionColorID, data.InteractionColor);


                    context.cmd.DrawProcedural(Matrix4x4.identity, composeMaterial, 0, MeshTopology.Triangles, 3);
                });
            }
        }


        private class CutoutData
        {
            internal ComputeShader ComputeShader;
            internal TextureHandle MaskTexture;
            internal TextureHandle ResultTexture;
            internal TextureHandle SceneDepthTexture;
            internal TextureHandle StencilTexture;
            internal int3 TileCount;
        }

        private class BlurData
        {
            internal TextureHandle MaskTexture;
            internal float RadiusInPixels;
            internal TextureHandle ResultTexture;
        }

        private class ComposeData
        {
            internal Color EnemyColor;
            internal Color FriendColor;
            internal Color InteractionColor;
            internal Color NeutralColor;
            internal float RadiusInPixels;
            internal TextureHandle ResultTexture;
            internal TextureHandle SceneDepthTexture;
        }
    }
}