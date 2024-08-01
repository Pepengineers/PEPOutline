Shader "RenderFeature/ComposeOutline"
{
    Properties
    {
        _HiddenMultiply ("Hidden Multiply", Float) = 0.4
    }

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
            Name "ComposeOutline"
            Blend SrcAlpha OneMinusSrcAlpha
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
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            float _HiddenMultiply;
            float4 _FriendColor;
            float4 _EnemyColor;
            float4 _NeutralColor;
            float4 _InteractionColor;
            TYPED_TEXTURE2D(float4, _ResultTexture);
            TYPED_TEXTURE2D(float, _DepthTexture);

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

            float4 GetColor(float3 MainColor, float Distance)
            {
                Distance = saturate((((Distance * 2 - 1)) + 1) / 2);
                float Alpha = saturate(Distance * (1 - Distance) * 4);
                Alpha *= 0.8f;
                return float4(MainColor, Alpha);
            }

            float4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float2 Pos = input.texcoord.xy;

                // r - Stencil
                // g - Custom Depth
                // b - Blurred Value
                const float4 SampleTex = _ResultTexture.Sample(sampler_TrilinearClamp, Pos);
                const float SceneDepth = _DepthTexture.Sample(sampler_TrilinearClamp, Pos);
                const uint Stencil = SampleTex.r;
                const float CustomDepth = SampleTex.g;
                const float DistanceAlpha = SampleTex.b;

                float4 Color = 0;

                float ShowFriendColor = (Stencil >> 1) & 1;
                float ShowEnemyColor = (Stencil >> 2) & 1;
                float ShowNeutralColor = (Stencil >> 3) & 1;
                float ShowInteractionColor = (Stencil >> 4) & 1;
                float ShowAlpha = min(1, ShowFriendColor + ShowEnemyColor + ShowNeutralColor + ShowInteractionColor);

                Color = lerp(Color, _FriendColor, ShowFriendColor);
                Color = lerp(Color, _EnemyColor, ShowEnemyColor);
                Color = lerp(Color, _NeutralColor, ShowNeutralColor);
                Color = lerp(Color, _InteractionColor, ShowInteractionColor);

                float4 ResultColor = GetColor(Color.rgb, DistanceAlpha);
                ResultColor.a *= ShowAlpha;


                if (SceneDepth < CustomDepth * 1.02f)
                {
                    return ResultColor;
                }

                float4 HiddenColor = Color;
                HiddenColor.a *= _HiddenMultiply;
                HiddenColor.a = lerp(HiddenColor.a, ResultColor.a, ResultColor.a);

                return HiddenColor;
            }
            ENDHLSL
        }
    }
}