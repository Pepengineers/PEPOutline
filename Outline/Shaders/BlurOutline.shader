Shader "RenderFeature/BlurOutline"
{
    SubShader
    {
        Tags
        {
            "RenderType"="Opaque" "RenderPipeline" = "UniversalPipeline"
        }
        ZWrite Off Cull Off
        ColorMask RGBA
        Pass
        {
            Name "BlurOutlinePass"
            Blend One Zero
            BlendOp Add

            HLSLPROGRAM
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/DynamicScaling.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/UnityInstancing.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/GlobalSamplers.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/EntityLighting.hlsl"

            float _RadiusInPixels;
            int _TileSize;
            TYPED_TEXTURE2D(float4, _ResultTexture);
            TYPED_TEXTURE2D(uint, _Mask);

            struct Attributes
            {
                uint vertexID : SV_VertexID;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            #pragma vertex Vert
            #pragma fragment Frag

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                output.positionCS = GetFullScreenTriangleVertexPosition(input.vertexID);
                output.texcoord = GetFullScreenTriangleTexCoord(input.vertexID);

                return output;
            }

            float4 Blur(in float4 PixelSampleValue, in Texture2D OutlineTexture, in int2 Pos, in float Radius)
            {
                float MinDistance = Radius * Radius + 1;

                float Depth = FLT_MAX;

                float Stencil = PixelSampleValue.r;
                float WeightStencil = 0;
                float WeightTotal = 0;

                UNITY_LOOP
                for (int i = -Radius; i < Radius + 1; i++)
                {
                    UNITY_LOOP
                    for (int j = -Radius; j < Radius + 1; j++)
                    {
                        int2 SamplePos = Pos + int2(i, j);

                        int R2 = i * i + j * j;

                        float Weight = Radius * Radius * 2 - R2;
                        WeightTotal += Weight;

                        // r - Stencil
                        // g - Custom Depth
                        // b - Blurred Value
                        // a - not used
                        float4 TexSample = OutlineTexture[SamplePos];

                        Stencil = max(Stencil, TexSample.r);
                        WeightStencil += (TexSample.r > 0) * Weight;

                        if ((TexSample.r > 0) && (MinDistance > R2))
                        {
                            MinDistance = R2;
                            Depth = TexSample.g;
                        }
                    }
                }
                return float4(Stencil, Depth, WeightStencil / WeightTotal, PixelSampleValue.a);
            }

            float4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float2 Pos = input.positionCS.xy;
                uint2 TilePos = Pos.xy / (_TileSize - 1);

                uint TileMaskValue = _Mask[TilePos];

                float4 TexSample = _ResultTexture[Pos];

                // Early out of blur if there is no outline in a tile
                UNITY_BRANCH
                if (TileMaskValue == 0)
                {
                    return TexSample;
                }

                return Blur(TexSample, _ResultTexture, Pos, _RadiusInPixels);
            }
            ENDHLSL
        }
    }
}