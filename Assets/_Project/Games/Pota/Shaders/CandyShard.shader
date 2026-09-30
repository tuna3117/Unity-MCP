// A's candy chunks: the particle colour (linear, per particle) with a two-tone toon ramp, a rim and an inverted-hull ink outline.
Shader "Project/CandyShard"
{
    Properties
    {
        _ShadeColor ("Shade (multiplies the tone in shadow)", Color) = (0.62, 0.6, 0.78, 1)
        _ShadeThreshold ("Shade Threshold", Range(-1, 1)) = 0.15
        _RimStrength ("Rim", Range(0, 1)) = 0.35
        _OutlineColor ("Outline", Color) = (0.078, 0.106, 0.18, 1)
        _OutlineWidth ("Outline Width (m)", Range(0, 0.1)) = 0.022
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" "RenderPipeline" = "UniversalPipeline" }
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        CBUFFER_START(UnityPerMaterial)
            half4 _ShadeColor;
            half _ShadeThreshold;
            half _RimStrength;
            half4 _OutlineColor;
            float _OutlineWidth;
        CBUFFER_END
        ENDHLSL

        Pass
        {
            Name "CandyForward"
            Tags { "LightMode" = "UniversalForward" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; half4 color : COLOR; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 normalWS : TEXCOORD0; float3 positionWS : TEXCOORD1; half4 color : COLOR; };

            Varyings vert(Attributes v)
            {
                Varyings o;
                VertexPositionInputs p = GetVertexPositionInputs(v.positionOS.xyz);
                o.positionCS = p.positionCS;
                o.positionWS = p.positionWS;
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.color = v.color;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float3 n = normalize(i.normalWS);
                Light light = GetMainLight();
                half lit = smoothstep(_ShadeThreshold - 0.03h, _ShadeThreshold + 0.03h, dot(n, light.direction));
                half3 tone = i.color.rgb;
                half3 color = tone * lerp(_ShadeColor.rgb, half3(1, 1, 1), lit) * light.color;
                color += tone * SampleSH(n) * 0.35h;
                float3 viewDir = normalize(GetWorldSpaceViewDir(i.positionWS));
                color += pow(1.0h - saturate(dot(n, viewDir)), 3.0h) * _RimStrength * lit;
                return half4(color, 1);
            }
            ENDHLSL
        }

        Pass
        {
            Name "Outline"
            Tags { "LightMode" = "SRPDefaultUnlit" }
            Cull Front
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings { float4 positionCS : SV_POSITION; };

            Varyings vert(Attributes v)
            {
                Varyings o;
                float3 p = TransformObjectToWorld(v.positionOS.xyz) + normalize(TransformObjectToWorldNormal(v.normalOS)) * _OutlineWidth;
                o.positionCS = TransformWorldToHClip(p);
                return o;
            }

            half4 frag(Varyings i) : SV_Target { return _OutlineColor; }
            ENDHLSL
        }
    }
}
