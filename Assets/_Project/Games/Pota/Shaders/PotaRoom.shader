// three.js RoomEnvironment materials, for the Editor-only reflection bake (PotaRoomBaker): MeshStandardMaterial white, diffuse part,
// lit by PointLight(900 cd, distance 28, decay 2) with three's falloff 1/d² · saturate(1 − (d/r)⁴)²; the panels are
// MeshBasicMaterial colour 17–100 (HDR). No Unity lights, no ambient: exactly the room three renders into its PMREM.
Shader "Project/PotaRoom"
{
    Properties
    {
        _Albedo ("Albedo", Float) = 1
        _Emission ("Emission", Float) = 0
        _LightPos ("Light position (world)", Vector) = (0, 0, 0, 0)
        _LightIntensity ("Light intensity (cd)", Float) = 900
        _LightRange ("Light range", Float) = 28
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 2
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
        Pass
        {
            Name "Room"
            Tags { "LightMode" = "UniversalForward" }
            Cull [_Cull]
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float _Albedo;
                float _Emission;
                float4 _LightPos;
                float _LightIntensity;
                float _LightRange;
                float _Cull;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; float3 normalWS : TEXCOORD1; };

            Varyings vert(Attributes i)
            {
                Varyings o;
                o.positionWS = TransformObjectToWorld(i.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.normalWS = TransformObjectToWorldNormal(i.normalOS);
                return o;
            }

            float4 frag(Varyings i, bool front : SV_IsFrontFace) : SV_Target
            {
                float3 n = normalize(i.normalWS) * (front ? 1.0 : -1.0);   // BackSide: three flips the normal of back faces
                float3 l = _LightPos.xyz - i.positionWS;
                float d = length(l);
                float ndl = saturate(dot(n, l / max(d, 1e-4)));
                float win = saturate(1.0 - pow(d / _LightRange, 4.0));
                float lit = _Albedo * INV_PI * _LightIntensity * ndl * win * win / max(d * d, 0.01);
                return float4((lit + _Emission).xxx, 1.0);
            }
            ENDHLSL
        }
    }
}
