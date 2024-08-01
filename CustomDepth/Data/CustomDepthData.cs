using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;

namespace Pepengineers.PEPOutline.CustomDepth.Data
{
    internal sealed class CustomDepthData : ContextItem
    {
        public TextureHandle CustomDepth { get; internal set; }

        public override void Reset()
        {
            CustomDepth = TextureHandle.nullHandle;
        }
    }
}