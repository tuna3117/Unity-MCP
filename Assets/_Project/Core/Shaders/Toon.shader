Shader "Project/Toon"
{
    Properties
    {
        [MainTexture] _BaseMap ("Base Map", 2D) = "white" {}
        [MainColor] _BaseColor ("Base Color", Color) = (1, 1, 1, 1)
        _ShadeColor ("Shade Color (multiplies the base in shadow)", Color) = (0.72, 0.68, 0.82, 1)
        _ShadeThreshold ("Shade Threshold", Range(-1, 1)) = 0.1
        _ShadeSoftness ("Shade Softness", Range(0.001, 0.5)) = 0.04
        _RimColor ("Rim Color", Color) = (1, 1, 1, 1)
        _RimStrength ("Rim Strength", Range(0, 1)) = 0.35
        _RimPower ("Rim Power", Range(0.5, 8)) = 3
        _OutlineColor ("Outline Color", Color) = (0.08, 0.06, 0.1, 1)
        _OutlineWidth ("Outline Width (m)", Range(0, 0.05)) = 0.006
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            half4 _BaseColor;
            half4 _ShadeColor;
            half _ShadeThreshold;
            half _ShadeSoftness;
            half4 _RimColor;
            half _RimStrength;
            half _RimPower;
            half4 _OutlineColor;
            float _OutlineWidth;
        CBUFFER_END
        TEXTURE2D(_BaseMap);
        SAMPLER(sampler_BaseMap);
        ENDHLSL

        Pass
        {
            Name "ToonForward"
            Tags { "LightMode" = "UniversalForward" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float2 uv : TEXCOORD0; };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float3 positionWS : TEXCOORD2;
                half fog : TEXCOORD3;
            };

            Varyings vert(Attributes v)
            {
                Varyings o;
                VertexPositionInputs p = GetVertexPositionInputs(v.positionOS.xyz);
                o.positionCS = p.positionCS;
                o.positionWS = p.positionWS;
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.uv = TRANSFORM_TEX(v.uv, _BaseMap);
                o.fog = ComputeFogFactor(p.positionCS.z);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                half4 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv) * _BaseColor;
                float3 n = normalize(i.normalWS);
                Light light = GetMainLight(TransformWorldToShadowCoord(i.positionWS));
                half ndl = dot(n, light.direction);
                half lit = smoothstep(_ShadeThreshold - _ShadeSoftness, _ShadeThreshold + _ShadeSoftness, ndl)
                           * light.shadowAttenuation;
                half3 color = albedo.rgb * lerp(_ShadeColor.rgb, half3(1, 1, 1), lit) * light.color;
                color += albedo.rgb * SampleSH(n) * 0.35h;
                float3 viewDir = normalize(GetWorldSpaceViewDir(i.positionWS));
                half rim = pow(1.0h - saturate(dot(n, viewDir)), _RimPower) * _RimStrength * lit;
                color += _RimColor.rgb * rim;
                color = MixFog(color, i.fog);
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
                float3 positionWS = TransformObjectToWorld(v.positionOS.xyz)
                                    + TransformObjectToWorldNormal(v.normalOS) * _OutlineWidth;
                o.positionCS = TransformWorldToHClip(positionWS);
                return o;
            }

            half4 frag(Varyings i) : SV_Target { return _OutlineColor; }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On ZTest LEqual ColorMask 0 Cull Back
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            float3 _LightDirection;
            float3 _LightPosition;
            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };

            float4 vert(Attributes v) : SV_POSITION
            {
                float3 positionWS = TransformObjectToWorld(v.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(v.normalOS);
            #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                float3 lightDir = normalize(_LightPosition - positionWS);
            #else
                float3 lightDir = _LightDirection;
            #endif
                float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDir));
            #if UNITY_REVERSED_Z
                positionCS.z = min(positionCS.z, UNITY_NEAR_CLIP_VALUE);
            #else
                positionCS.z = max(positionCS.z, UNITY_NEAR_CLIP_VALUE);
            #endif
                return positionCS;
            }

            half4 frag() : SV_Target { return 0; }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On ColorMask R
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            struct Attributes { float4 positionOS : POSITION; };
            float4 vert(Attributes v) : SV_POSITION { return TransformObjectToHClip(v.positionOS.xyz); }
            half frag() : SV_Target { return 0; }
            ENDHLSL
        }
    }
}
