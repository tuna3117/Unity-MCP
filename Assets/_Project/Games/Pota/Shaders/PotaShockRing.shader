// B's shock ring: bends what is behind it (the camera's opaque texture) along the flipbook's screen normal (R = x, G = y, A = ring;
// linear), with a faint bright rim. Needs the opaque texture on the camera (ShatterBurst turns it on for its look only).
Shader "Project/PotaShockRing"
{
    Properties
    {
        _BaseMap ("Normal flipbook (linear)", 2D) = "black" {}
        _Strength ("Strength (screen UV)", Range(0, 0.1)) = 0.035
        _Brighten ("Rim brighten", Range(0, 1)) = 0.12
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Pass
        {
            Name "Shock"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float _Strength;
                half _Brighten;
            CBUFFER_END
            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);
            struct Attributes { float4 positionOS : POSITION; half4 color : COLOR; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; half4 color : COLOR; float2 uv : TEXCOORD0; float4 screen : TEXCOORD1; };

            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.color = v.color;
                o.uv = TRANSFORM_TEX(v.uv, _BaseMap);
                o.screen = ComputeScreenPos(o.positionCS);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                half4 t = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv);
                half mask = t.a * i.color.a;
                float2 n = t.rg * 2.0 - 1.0;
                float2 uv = i.screen.xy / i.screen.w + n * _Strength * mask;
                half3 scene = SampleSceneColor(uv);
                return half4(scene * (1.0h + _Brighten * mask), mask);
            }
            ENDHLSL
        }
    }
}
