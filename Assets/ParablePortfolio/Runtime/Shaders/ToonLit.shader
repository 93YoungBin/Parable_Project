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

                // ── Main Light ─────────────────────────────────────
                Light mainLight = GetMainLight(IN.shadowCoord);
                float NdotL     = dot(normalWS, mainLight.direction);

                // ── Ramp Cel-Shading ───────────────────────────────
                half ramp;
                #if _USE_RAMP_TEX
                    ramp = RampLightingTex(TEXTURE2D_ARGS(_RampMap, sampler_RampMap), NdotL);
                #else
                    ramp = RampLighting(NdotL);
                #endif

                half shadow  = mainLight.shadowAttenuation;
                float3 lit   = lerp(_ShadowColor.rgb, baseColor.rgb, ramp * shadow);
                lit         *= mainLight.color;

                // ── Blinn-Phong Specular (Step 기반) ──────────────
                float3 viewDir  = normalize(GetWorldSpaceViewDir(IN.positionWS));
                float3 halfDir  = normalize(mainLight.direction + viewDir);
                float  NdotH    = max(0, dot(normalWS, halfDir));
                float  specMask = step(1.0 - _SpecularSize, NdotH);
                lit += specMask * _SpecularIntensity * mainLight.color * shadow;

                // ── Additional Lights ──────────────────────────────
                #ifdef _ADDITIONAL_LIGHTS
                uint lightCount = GetAdditionalLightsCount();
                for (uint i = 0u; i < lightCount; ++i)
                {
                    Light addLight = GetAdditionalLight(i, IN.positionWS);
                    float addNdotL = max(0, dot(normalWS, addLight.direction));
                    lit += baseColor.rgb * addLight.color * addNdotL
                           * addLight.distanceAttenuation * addLight.shadowAttenuation * 0.5;
                }
                #endif

                half3 color = MixFog(lit, IN.fogFactor);
                return half4(color, baseColor.a);
            }
            ENDHLSL
        }

        // ─── Pass 1 : Outline (Inverted Hull, Screen-space 두께) ─────────
        Pass
        {
            Name "Outline"
            Tags { "LightMode"="SRPDefaultUnlit" }
            Cull Front

            HLSLPROGRAM
            #pragma vertex   vertOutline
            #pragma fragment fragOutline

            #include "Includes/ToonLitInput.hlsl"

            struct AttributesOutline
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct VaryingsOutline
            {
                float4 positionCS : SV_POSITION;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            VaryingsOutline vertOutline(AttributesOutline IN)
            {
                VaryingsOutline OUT;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);

                float4 posCS    = TransformObjectToHClip(IN.positionOS.xyz);
                float3 normalVS = mul((float3x3)UNITY_MATRIX_IT_MV, IN.normalOS);

                // Screen-space 균일 두께: UNITY_MATRIX_P로 직접 프로젝션
                float2 normalCS = normalize(float2(
                    UNITY_MATRIX_P[0][0] * normalVS.x,
                    UNITY_MATRIX_P[1][1] * normalVS.y));
                posCS.xy += normalCS * _OutlineWidth * posCS.w;

                OUT.positionCS = posCS;
                return OUT;
            }

            half4 fragOutline(VaryingsOutline IN) : SV_Target
            {
                return _OutlineColor;
            }
            ENDHLSL
        }

        // ─── Shadow Caster ───────────────────────────────────────────────
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