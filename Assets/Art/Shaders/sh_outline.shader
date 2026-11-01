// Comic-book outline: an inverted hull. The model is drawn a second time with its back faces only, every vertex pushed
// outwards along its SMOOTHED normal, so what peeks out around the silhouette is the outline. The push is made in clip
// space, so the line is the same number of pixels wide whatever the distance to the camera (a world-space push would
// swell as the camera zooms in on the crumbling arena and shrink to nothing far away).
//
// The smoothed normal is baked into TEXCOORD3 by OutlineNormalsBaker: the low-poly models have hard normals (one per
// face), and a hull pushed along them splits at every corner.
Shader "ToyCarArena/Outline"
{
    Properties
    {
        _Color ("Colour", Color) = (0.1137, 0.1686, 0.3098, 1)
        _Width ("Width, pixels at 1080 on the short screen side", Float) = 3
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        Pass
        {
            Name "Outline"
            Tags { "LightMode" = "SRPDefaultUnlit" }
            Cull Front
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 2.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                float _Width;
            CBUFFER_END

            // A camera that draws into a texture shown smaller than the screen (the result's winner podium) thickens the
            // line by this much for its own render; unset, it is 0 and counts as 1.
            float _OutlineBoost;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float3 smoothNormalOS : TEXCOORD3;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                float4 positionCS = TransformObjectToHClip(input.positionOS.xyz);

                // A model without baked normals falls back to its own, which is only a little worse.
                float3 normalOS = dot(input.smoothNormalOS, input.smoothNormalOS) > 0.0001 ? input.smoothNormalOS : input.normalOS;
                float3 normalWS = TransformObjectToWorldDir(normalOS);
                float2 direction = mul((float3x3)UNITY_MATRIX_VP, normalWS).xy;
                direction = normalize(direction + float2(1e-5, 0));

                // The width is a share of the short screen side, so a phone and a desktop show the same line.
                float2 screen = _ScreenParams.xy;
                float pixels = _Width * min(screen.x, screen.y) / 1080.0 * max(_OutlineBoost, 1.0);
                positionCS.xy += direction * (pixels * 2.0 / screen) * positionCS.w;

                output.positionCS = positionCS;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                return _Color;
            }
            ENDHLSL
        }
    }
}
