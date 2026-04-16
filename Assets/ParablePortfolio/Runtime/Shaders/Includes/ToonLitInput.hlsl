#ifndef TOON_LIT_INPUT_INCLUDED
#define TOON_LIT_INPUT_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

// ── Textures ──────────────────────────────────────────────────────────
TEXTURE2D(_BaseMap);        SAMPLER(sampler_BaseMap);
TEXTURE2D(_RampMap);        SAMPLER(sampler_RampMap);

// ── Per-Material CBuffer ──────────────────────────────────────────────
CBUFFER_START(UnityPerMaterial)
    float4 _BaseMap_ST;
    float4 _BaseColor;
    float4 _ShadowColor;
    float  _RampThreshold;
    float  _RampSmooth;
    float  _SpecularSize;
    float  _SpecularIntensity;
    float  _OutlineWidth;
    float4 _OutlineColor;
CBUFFER_END

// ── Ramp 샘플링 헬퍼 ─────────────────────────────────────────────────
// UseRampTex=1 이면 _RampMap 샘플링, 0 이면 smoothstep 사용
half RampLighting(float NdotL)
{
    // 글로벌 오버라이드(_ToonRampThreshold, _ToonRampSmooth)를 Feature에서 주입 가능
    float threshold = _RampThreshold;
    float smooth_   = _RampSmooth;
    return smoothstep(threshold - smooth_, threshold + smooth_, NdotL);
}

half RampLightingTex(TEXTURE2D_PARAM(rampTex, rampSampler), float NdotL)
{
    float u = saturate(NdotL * 0.5 + 0.5);
    return SAMPLE_TEXTURE2D(rampTex, rampSampler, float2(u, 0.5)).r;
}

#endif