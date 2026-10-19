// Invisible occlusion material for the scanned LiDAR mesh.
// Draws nothing you can see, but tells the GPU "a real surface is here",
// so paint behind a real wall gets hidden.
//
// Push Back: the invisible wall is moved this far AWAY from the camera (metres).
// This gives paint some tolerance, because the LiDAR mesh is a bit noisy and
// keeps updating. Paint on the wall stays visible; paint behind a real wall
// (thicker than Push Back) is still hidden.
Shader "Custom/ARDepthOnly"
{
    Properties
    {
        _PushBack ("Push Back (m)", Range(0, 0.1)) = 0.03
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry-1" "RenderPipeline"="UniversalPipeline" }

        Pass
        {
            ZWrite On
            ColorMask 0

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            float _PushBack;

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings   { float4 positionCS : SV_POSITION; };

            Varyings vert (Attributes v)
            {
                Varyings o;
                float3 posWS = TransformObjectToWorld(v.positionOS.xyz);
                float3 posVS = TransformWorldToView(posWS);

                // Move the point further away along the line from the camera,
                // so it stays at the same spot on screen but slightly deeper.
                float d = length(posVS);
                posVS *= (d + _PushBack) / max(d, 1e-4);

                o.positionCS = TransformWViewToHClip(posVS);
                return o;
            }

            half4 frag (Varyings i) : SV_Target { return 0; }
            ENDHLSL
        }
    }
}
