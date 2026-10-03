// F7 board clip (spec 2026-10-03): an invisible quad over the frame's inside that writes stencil 1; the board layer's RenderObjects
// passes draw only where the stencil is 1, so refill blocks above the frame are clipped at its top edge. No colour, no depth.
Shader "Project/PotaStencilMask"
{
    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry-10" "RenderPipeline" = "UniversalPipeline" }
        Pass
        {
            Name "StencilMask"
            Tags { "LightMode" = "UniversalForward" }
            ColorMask 0
            ZWrite Off
            ZTest Always
            Cull Off
            Stencil
            {
                Ref 1
                Comp Always
                Pass Replace
            }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _Unused;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; };

            Varyings vert(Attributes i)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(i.positionOS.xyz);
                return o;
            }

            half4 frag(Varyings i) : SV_Target { return 0; }
            ENDHLSL
        }
    }
}
