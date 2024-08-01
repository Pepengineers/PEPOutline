using Pepengineers.PEPOutline.CustomDepth.Passes;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Pepengineers.PEPOutline.CustomDepth
{
    [DisallowMultipleRendererFeature("Custom Depth")]
    [Tooltip("Custom Depth")]
    [SupportedOnRenderer(typeof(UniversalRendererData))]
    internal sealed class CustomDepthFeature : ScriptableRendererFeature
    {
        [SerializeField] [ResourcePath("CustomDepth/Shaders/CustomDepth.shader")]
        private Shader customDepthShader;

        [SerializeField] private EnumParameter<DepthFormat> depthFormat = new(DepthFormat.Depth_32, true);
        [SerializeField] private LayerMaskParameter layerMask = new(0, true);
        [SerializeField] private RenderingLayerMaskParameter renderingLayerMaskParameter = new(RenderingLayerMask.defaultRenderingLayerMask, true);
        private CustomDepthPass pass;


        public override void Create()
        {
            pass = new CustomDepthPass(RenderPassEvent.AfterRenderingOpaques,
                depthFormat.value, layerMask.value, renderingLayerMaskParameter.value,
                RenderQueueRange.opaque, customDepthShader);
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (renderingData.cameraData.cameraType != CameraType.Game && renderingData.cameraData.cameraType != CameraType.SceneView) return;
            if (renderingData.cameraData.requiresDepthTexture == false || renderingData.cameraData.requiresOpaqueTexture == false)
            {
                Debug.LogWarning("Depth Texture or Opaque Texture was not enabled");
                return;
            }

            renderer.EnqueuePass(pass);
        }
    }
}