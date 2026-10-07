// The calm idles' overlays on a resting special ball (spec 2026-10-07), one additive quad each, white Blender stills tinted per instance:
//   _Mode 1 · B glint: the label atlas cell of the ball's sticker (_UvRect) masks a streak (_StreakTex) that sweeps across the quad
//            (_Sweep along the direction _Angle, quad units from the centre); the streak's height spans _Span, its length _Length.
//   _Mode 2 · C rim:   the ring still (_BaseMap, ring at _TexRadius, soft core _TexHalf, frame units) remapped radially so its
//            centre line sits at _Radius and its core at _Half (quad units): radius and thickness tune apart from the texture.
//   _Mode 0 · D twinkle: the star still as it is.
// Output rgb = _Color.rgb × _Color.a × mask, blended One One (pure light, nothing is darkened); no depth write.
Shader "Project/PotaGlint"
{
    Properties
    {
        _BaseMap ("Atlas (glint) / still (rim, twinkle)", 2D) = "white" {}
        _StreakTex ("Glint streak", 2D) = "white" {}
        _Mode ("Mode (0 sprite, 1 glint, 2 rim)", Float) = 0
        _Color ("Colour (linear) × strength in alpha", Color) = (1, 1, 1, 1)
        _UvRect ("Glint: the sticker's atlas cell (u, v, w, h)", Vector) = (0, 0, 1, 1)
        _Sweep ("Glint: band centre (quad units from the centre)", Float) = -2
        _Angle ("Glint: sweep direction (radians, quad space)", Float) = 0
        _Span ("Glint: streak texture height (quad units)", Float) = 1
        _Length ("Glint: streak texture width (quad units)", Float) = 1.25
        _Radius ("Rim: ring radius (quad units)", Float) = 0.367
        _Half ("Rim: half the soft core (quad units)", Float) = 0.0255
        _TexRadius ("Rim: the still's ring radius (frame units)", Float) = 0.4
        _TexHalf ("Rim: half the still's soft core (frame units)", Float) = 0.0215
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" "PreviewType" = "Plane" }
        Pass
        {
            Name "Glint"
            Tags { "LightMode" = "UniversalForward" }
            Blend One One
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);
            TEXTURE2D(_StreakTex);
            SAMPLER(sampler_StreakTex);
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _StreakTex_ST;
                float _Mode;
                float4 _Color;
                float4 _UvRect;
                float _Sweep;
                float _Angle;
                float _Span;
                float _Length;
                float _Radius;
                float _Half;
                float _TexRadius;
                float _TexHalf;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };

            Varyings vert(Attributes i)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(i.positionOS.xyz);
                o.uv = i.uv;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float2 p = i.uv - 0.5;
                half mask;
                if (_Mode > 1.5)
                {
                    float r = length(p);
                    float rt = _TexRadius + (r - _Radius) * (_TexHalf / max(_Half, 1e-4));
                    float2 dir = r > 1e-5 ? p / r : float2(1, 0);
                    mask = rt > 0 ? SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, 0.5 + dir * rt).a : 0;
                }
                else if (_Mode > 0.5)
                {
                    float2 across = float2(cos(_Angle), sin(_Angle));
                    float2 along = float2(-across.y, across.x);
                    float2 s = float2(dot(p, along) / _Length, (dot(p, across) - _Sweep) / max(_Span, 1e-4)) + 0.5;
                    half streak = all(saturate(s) == s) ? SAMPLE_TEXTURE2D(_StreakTex, sampler_StreakTex, s).a : 0;
                    half sticker = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, _UvRect.xy + i.uv * _UvRect.zw).a;
                    mask = streak * sticker;
                }
                else mask = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv).a;
                return half4(_Color.rgb * (_Color.a * mask), 0);
            }
            ENDHLSL
        }
    }
}
