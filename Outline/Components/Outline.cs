using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Pepengineers.PEPOutline.Outline.Components
{
    [Serializable]
    [VolumeComponentMenu("Post-processing/Outline")]
    [SupportedOnRenderPipeline(typeof(UniversalRenderPipelineAsset))]
    internal sealed class Outline : VolumeComponent, IPostProcessComponent
    {
        [Header("Settings:")] public BoolParameter Enable = new(false, true);

        public ColorParameter EnemyColor = new(Color.red, true) { hdr = true };
        public ColorParameter FriendColor = new(Color.green, true) { hdr = true };
        public ColorParameter NeutralColor = new(Color.yellow, true) { hdr = true };
        public ColorParameter InteractionColor = new(Color.white, true) { hdr = true };
        public ClampedIntParameter RadiusInPixels = new(2, 1, 10, true);

        public bool IsActive()
        {
            return Enable.value;
        }
    }
}