// Pota's label atlas quads (yapbozSahne.ts EtiketKatmani): atlas cell per instance (_UvRect), tint per instance (_Color, linear).
// Straight alpha like three's NormalBlending; GPU instancing keeps all block labels in a couple of draws.
Shader "Project/PotaLabel"
{
    Properties
    {
        _BaseMap ("Atlas", 2D) = "white" {}
        _UvRect ("UV rect (u, v, w, h)", Vector) = (0, 0, 1, 1)
        _Color ("Tint", Color) = (1, 1, 1, 1)
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Pass
        {
            Name "Label"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
            CBUFFER_END
            UNITY_INSTANCING_BUFFER_START(Props)
                UNITY_DEFINE_INSTANCED_PROP(float4, _UvRect)
                UNITY_DEFINE_INSTANCED_PROP(float4, _Color)
            UNITY_INSTANCING_BUFFER_END(Props)

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; half4 color : COLOR; };

            Varyings vert(Attributes i)
            {
                UNITY_SETUP_INSTANCE_ID(i);
                Varyings o;
                o.positionCS = TransformObjectToHClip(i.positionOS.xyz);
                float4 r = UNITY_ACCESS_INSTANCED_PROP(Props, _UvRect);
                o.uv = r.xy + i.uv * r.zw;
                o.color = UNITY_ACCESS_INSTANCED_PROP(Props, _Color);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                half4 t = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv);
                return half4(t.rgb * i.color.rgb, t.a * i.color.a);
            }
            ENDHLSL
        }
    }
}
