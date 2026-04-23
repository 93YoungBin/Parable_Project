Shader "Parable/ToonLit"
{
    Properties
    {
        [Header(Base)]
        _BaseColor      ("Base Color",    Color)        = (1,1,1,1)
        _BaseMap        ("Base Map",      2D)           = "white" {}

        [Header(Cel Shading)]
        _ShadowColor    ("Shadow Color",  Color)        = (0.25,0.28,0.4,1)
        _RampMap        ("Ramp Map (opt)",2D)           = "white" {}
        [Toggle(_USE_RAMP_TEX)] _UseRampTex ("Use Ramp Texture", Float) = 0
        _RampThreshold  ("Ramp Threshold",Range(0,1))   = 0.5
        _RampSmooth     ("Ramp Smooth",   Range(0,0.2)) = 0.04

        [Header(Specular)]
        _SpecularSize      ("Specular Size",      Range(0,1)) = 0.1
        _SpecularIntensity ("Specular Intensity", Range(0,2)) = 0.5

        [Header(Outline)]
        _OutlineWidth   ("Outline Width", Range(0,0.05)) = 0.005
        _OutlineColor   ("Outline Color", Color)         = (0.05,0.05,0.05,1)
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }

        // ─── Pass 0 : ToonLit ────────────────────────────────────────────
        Pass
        {
            Name "ToonLit"
            Tags { "LightMode"="UniversalForward" }

            HLSLPROGRAM
            #pragma vertex   vert
            #pragma fragment frag

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _SHADOWS_SOFT
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fog
            #pragma shader_feature_local _USE_RAMP_TEX

            #include "Includes/ToonLitInput.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS  : SV_POSITION;
                float2 uv          : TEXCOORD0;
                float3 normalWS    : TEXCOORD1;
                float3 positionWS  : TEXCOORD2;
                float4 shadowCoord : TEXCOORD3;
                float  fogFactor   : TEXCOORD4;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);

                VertexPositionInputs posInputs  = GetVertexPositionInputs(IN.positionOS.xyz);
                VertexNormalInputs   normInputs = GetVertexNormalInputs(IN.normalOS);

                OUT.positionCS  = posInputs.positionCS;
                OUT.positionWS  = posInputs.positionWS;
                OUT.normalWS    = normInputs.normalWS;
                OUT.uv          = TRANSFORM_TEX(IN.uv, _BaseMap);
                OUT.shadowCoord = GetShadowCoord(posInputs);
                OUT.fogFactor   = ComputeFogFactor(posInputs.positionCS.z);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                half4 baseColor = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, IN.uv) * _BaseColor;
                float3 normalWS = normalize(IN.normalWS);
                float3 viewDir  = normalize(GetWorldSpaceViewDir(IN.positionWS));

                // ── Main Light ─────────────────────────────────────
                Light mainLight = GetMainLight(IN.shadowCoord);

                // ── 뷰 기반 셀 팩터 ────────────────────────────────
                // NdotV: 카메라를 향하는 면=1(lit), 옆/뒷면=0(shadow)
                // → 정면은 텍스처 원색, 실루엣 테두리만 shadow color
                float NdotV    = saturate(dot(normalWS, viewDir));
                half  viewRamp = smoothstep(_RampThreshold, _RampThreshold + _RampSmooth, NdotV);

                // ── 라이트 기반 cast shadow 보정 ────────────────────
                // NdotL은 cast shadow(다른 물체에 의한 그림자)에만 약하게 사용
                float NdotL    = dot(normalWS, mainLight.direction);
                half  ramp;
                #if _USE_RAMP_TEX
                    ramp = RampLightingTex(TEXTURE2D_ARGS(_RampMap, sampler_RampMap), NdotL);
                #else
                    ramp = RampLighting(NdotL);
                #endif
                half shadow = mainLight.shadowAttenuation;
                // cast shadow만 반영 (라이트 각도로 인한 어두움은 최소화)
                half lightMod = max(ramp * shadow, 0.45);

                // ── 최종 셀 팩터 합성 ────────────────────────────────
                // 뷰 기반(실루엣) × 라이트(cast shadow만) → 자연스러운 셀 셰이딩
                half celFactor = viewRamp * lightMod;
                float3 cel     = lerp(_ShadowColor.rgb, baseColor.rgb, celFactor);

                // ── Specular ─────────────────────────────────────────
                float3 halfDir  = normalize(mainLight.direction + viewDir);
                float  NdotH    = max(0, dot(normalWS, halfDir));
                float  specMask = step(1.0 - _SpecularSize, NdotH) * shadow * viewRamp;
                cel += specMask * _SpecularIntensity * 0.6;

                half3 color = MixFog(cel, IN.fogFactor);
                return half4(color, baseColor.a);
            }
            ENDHLSL
        }

        // ─── Shadow Caster ───────────────────────────────────────────────
        // NOTE: Outline 패스는 ToonRendererFeature + ToonOutlineReplace.shader 조합의
        //       레이어 기반 override로 이관됨. 이 셰이더는 순수 Ramp Lit 역할만 담당한다.
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }
            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull Back

            HLSLPROGRAM
            #pragma vertex   vertShadow
            #pragma fragment fragShadow
            #pragma multi_compile_shadowcaster

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

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

            float3 _LightDirection;

            struct AttrShadow  { float4 positionOS:POSITION; float3 normalOS:NORMAL; };
            struct VarysShadow { float4 positionCS:SV_POSITION; };

            float4 GetShadowClipPos(float3 posWS, float3 normWS)
            {
                // 노멀 방향으로 소폭 bias
                float4 posCS = TransformWorldToHClip(posWS + normWS * 0.005);
                #if UNITY_REVERSED_Z
                    posCS.z = min(posCS.z, posCS.w * UNITY_NEAR_CLIP_VALUE);
                #else
                    posCS.z = max(posCS.z, posCS.w * UNITY_NEAR_CLIP_VALUE);
                #endif
                return posCS;
            }

            VarysShadow vertShadow(AttrShadow IN)
            {
                VarysShadow OUT;
                float3 posWS  = TransformObjectToWorld(IN.positionOS.xyz);
                float3 normWS = TransformObjectToWorldNormal(IN.normalOS);
                OUT.positionCS = GetShadowClipPos(posWS, normWS);
                return OUT;
            }

            half4 fragShadow(VarysShadow IN) : SV_Target { return 0; }
            ENDHLSL
        }

        // ─── Depth Only ──────────────────────────────────────────────────
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }
            ZWrite On
            ColorMask R
            Cull Back

            HLSLPROGRAM
            #pragma vertex   vertDepth
            #pragma fragment fragDepth

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

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

            struct AttrDepth  { float4 positionOS:POSITION; };
            struct VarysDepth { float4 positionCS:SV_POSITION; };

            VarysDepth vertDepth(AttrDepth IN)
            {
                VarysDepth OUT;
                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                return OUT;
            }
            half4 fragDepth(VarysDepth IN) : SV_Target { return 0; }
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}