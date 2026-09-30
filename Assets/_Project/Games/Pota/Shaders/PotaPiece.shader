// B's broken pieces: the block tone (linear, per renderer) lit by the main light, SH and the room probe, with the sticker (item and
// number atlas cells at BlockView's placement) on the block's original front faces (UV1.x = 1). Face UV (UV0) runs 0..1 across the face.
Shader "Project/PotaPiece"
{
    Properties
    {
        [MainColor] _BaseColor ("Tone (linear, per renderer)", Color) = (1, 1, 1, 1)
        _Smoothness ("Smoothness", Range(0, 1)) = 0.84
        _Reflection ("Reflection", Range(0, 1)) = 0.45
        _LabelAtlas ("Label Atlas", 2D) = "black" {}
        _ItemRect ("Item cell (atlas UV rect)", Vector) = (0, 0, 0, 0)
        _NumRect ("Number cell (atlas UV rect)", Vector) = (0, 0, 0, 0)
        _ItemPlace ("Item place on the face (centre xy, size zw)", Vector) = (0.5, 0.567, 0.836, 0.836)
        _NumPlace ("Number place on the face (centre xy, size zw)", Vector) = (0.789, 0.189, 0.45, 0.45)
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" "RenderPipeline" = "UniversalPipeline" }
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        CBUFFER_START(UnityPerMaterial)
            half4 _BaseColor;
            half _Smoothness;
            half _Reflection;
            float4 _LabelAtlas_ST;
            float4 _ItemRect;
            float4 _NumRect;
            float4 _ItemPlace;
            float4 _NumPlace;
        CBUFFER_END
        TEXTURE2D(_LabelAtlas);
        SAMPLER(sampler_LabelAtlas);
        ENDHLSL

        Pass
        {
            Name "PieceForward"
            Tags { "LightMode" = "UniversalForward" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float2 uv : TEXCOORD0; float2 front : TEXCOORD1; };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float2 uv : TEXCOORD2;
                float front : TEXCOORD3;
                half fog : TEXCOORD4;
            };

            Varyings vert(Attributes v)
            {
                Varyings o;
                VertexPositionInputs p = GetVertexPositionInputs(v.positionOS.xyz);
                o.positionCS = p.positionCS;
                o.positionWS = p.positionWS;
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.uv = v.uv;
                o.front = v.front.x;
                o.fog = ComputeFogFactor(p.positionCS.z);
                return o;
            }

            half4 Label(float2 face, float4 place, float4 rect)
            {
                float2 local = (face - place.xy) / place.zw + 0.5;
                half4 c = SAMPLE_TEXTURE2D(_LabelAtlas, sampler_LabelAtlas, rect.xy + saturate(local) * rect.zw);
                return all(local >= 0.0) && all(local <= 1.0) ? c : half4(0, 0, 0, 0);
            }

            half4 frag(Varyings i) : SV_Target
            {
                half3 albedo = _BaseColor.rgb;
                if (i.front > 0.5)
                {
                    half4 item = Label(i.uv, _ItemPlace, _ItemRect);
                    albedo = lerp(albedo, item.rgb, item.a);
                    half4 number = Label(i.uv, _NumPlace, _NumRect);
                    albedo = lerp(albedo, number.rgb, number.a);
                }
                float3 n = normalize(i.normalWS);
                float3 viewDir = normalize(GetWorldSpaceViewDir(i.positionWS));
                Light light = GetMainLight(TransformWorldToShadowCoord(i.positionWS));
                half3 lightColor = light.color * light.shadowAttenuation;
                half3 color = albedo * (lightColor * saturate(dot(n, light.direction)) + SampleSH(n));
                half3 h = normalize(light.direction + viewDir);
                color += lightColor * pow(saturate(dot(n, h)), exp2(10.0 * _Smoothness + 1.0)) * _Smoothness * 0.5h;
                half fresnel = 0.04h + 0.96h * pow(1.0h - saturate(dot(n, viewDir)), 5.0h);
                color += GlossyEnvironmentReflection(reflect(-viewDir, n), 1.0h - _Smoothness, 1.0h) * fresnel * _Reflection;
            #if defined(_ADDITIONAL_LIGHTS) || USE_CLUSTER_LIGHT_LOOP
                // the burst's point light (spec §2 B) lights the pieces as it lights the blocks around them
                InputData inputData = (InputData)0;
                inputData.positionWS = i.positionWS;
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(i.positionCS);
                uint count = GetAdditionalLightsCount();
                LIGHT_LOOP_BEGIN(count)
                    Light extra = GetAdditionalLight(lightIndex, i.positionWS);
                    color += albedo * extra.color * (extra.distanceAttenuation * saturate(dot(n, extra.direction)));
                LIGHT_LOOP_END
            #endif
                return half4(MixFog(color, i.fog), 1);
            }
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
    }
}
