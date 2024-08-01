using Pepengineers.PEPOutline.CustomDepth;
using Pepengineers.PEPOutline.Outline.Passes;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Pepengineers.PEPOutline.Outline
{
    [DisallowMultipleRendererFeature("Outline")]
    [Tooltip("Outline")]
    [SupportedOnRenderer(typeof(UniversalRendererData))]
    [VolumeRequiresRendererFeatures(typeof(CustomDepthFeature))]
    internal sealed class OutlineFeature : ScriptableRendererFeature
    {
        [Header("Shaders")]
        [SerializeField] [ResourcePath("Outline/Shaders/CutoutOutline.compute")]
        private ComputeShader cutOutCS;

        [SerializeField] [ResourcePath("Outline/Shaders/BlurOutline.shader")]
        private Shader blurShader;

        [SerializeField] [ResourcePath("Outline/Shaders/ComposeOutline.shader")]
        private Shader composeShader;

        private Material blurMaterial;
        private Material composeMaterial;
        private OutlinePass outlinePass;

        public override void Create()
        {
            if (!SystemInfo.supportsComputeShaders)
            {
                Debug.LogWarning("Device does not support compute shaders. The pass will be skipped.");
                return;
            }

            // Skip the render pass if the compute shader is null.
            if (cutOutCS == null || blurShader == null || composeShader == null)
            {
                Debug.LogWarning("Any shader is null. The pass will be skipped.");
                return;
            }

            blurMaterial = CoreUtils.CreateEngineMaterial(blurShader);
            composeMaterial = CoreUtils.CreateEngineMaterial(composeShader);
            outlinePass = new OutlinePass(cutOutCS, blurMaterial, composeMaterial);
        }

        protected override void Dispose(bool disposing)
        {
            CoreUtils.Destroy(blurMaterial);
            CoreUtils.Destroy(composeMaterial);
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (!SystemInfo.supportsComputeShaders)
            {
                Debug.LogWarning("Device does not support compute shaders. The pass will be skipped.");
                return;
            }

            // Skip the render pass if the compute shader is null.
            if (cutOutCS == null || blurShader == null || composeShader == null)
            {
                Debug.LogWarning("Any shader is null. The pass will be skipped.");
                return;
            }

            if (renderingData.cameraData.cameraType != CameraType.Game && renderingData.cameraData.cameraType != CameraType.SceneView) return;
            if (renderingData.cameraData.requiresDepthTexture == false || renderingData.cameraData.requiresOpaqueTexture == false)
            {
                Debug.LogWarning("Depth Texture or Opaque Texture was not enabled");
                return;
            }

            renderer.EnqueuePass(outlinePass);
        }
    }
}