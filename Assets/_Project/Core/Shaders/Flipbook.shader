// Studio flipbook: a Blender vfx_flipbook sheet on Shuriken particles (Texture Sheet Animation picks the cell), tinted by the
// particle colour. Straight alpha or additive via _SrcBlend/_DstBlend (FlipbookMaterial.SetBlend). Vertex colour is 8-bit, so
// HDR brightness for bloom comes from _Intensity. No frame blending: toon sheets are meant to read frame by frame.
Shader "Project/Flipbook"
{
    Properties
    {
        _BaseMap ("Sheet", 2D) = "white" {}
        _Intensity ("Intensity (HDR)", Float) = 1
        [HideInInspector] _SrcBlend ("Src Blend", Float) = 5    // SrcAlpha
        [HideInInspector] _DstBlend ("Dst Blend", Float) = 10   // OneMinusSrcAlpha (Additive: One)
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 0
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" "PreviewType" = "Plane" }
        Pass
        {
            Name "Flipbook"
            Tags { "LightMode" = "UniversalForward" }
            Blend [_SrcBlend] [_DstBlend]
            ZWrite Off
            Cull [_Cull]
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half _Intensity;
                half _SrcBlend;
                half _DstBlend;
                half _Cull;
            CBUFFER_END
            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);
            struct Attributes { float4 positionOS : POSITION; half4 color : COLOR; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; half4 color : COLOR; float2 uv : TEXCOORD0; };

            Varyings vert(Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.color = v.color;
                o.uv = TRANSFORM_TEX(v.uv, _BaseMap);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                half4 t = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv);
                return half4(t.rgb * i.color.rgb * _Intensity, t.a * i.color.a);
            }
            ENDHLSL
        }
    }
}
