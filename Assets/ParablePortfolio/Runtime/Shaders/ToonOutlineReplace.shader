Shader "Parable/ToonOutlineReplace"
{
    // Layer 기반 ScriptableRendererFeature에서 override material로 사용되는 전용 셰이더.
    // 오브젝트의 원본 머티리얼과 무관하게, "Toon" 레이어에 속한 렌더러를 이 셰이더로
    // 다시 드로우하여 Inverted Hull 방식의 스크린스페이스 균일 아웃라인을 뽑는다.
    //
    // 참고: 이 셰이더는 단독 머티리얼로 오브젝트에 직접 할당해서 쓰는 용도가 아니다.
    //       ToonRendererFeature의 outlineOverrideMaterial 슬롯에만 할당된다.

    Properties
    {
        _OutlineColor ("Outline Color", Color)         = (0.05, 0.05, 0.05, 1)
        _OutlineWidth ("Outline Width", Range(0, 0.05)) = 0.005
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }

        Pass
        {
            Name "ToonOutline"
            // UniversalForward 태그: DrawRenderers가 동일 태그로 override 매칭
            Tags { "LightMode"="UniversalForward" }

            Cull Front      // Inverted Hull: 안쪽 면만 남겨 두꺼운 외곽선으로 사용
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM
            #pragma vertex   vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _OutlineColor;
                float  _OutlineWidth;
            CBUFFER_END

            // Feature가 주입하는 글로벌 오버라이드. > 0 이면 머티리얼 값 대신 사용.
            float _ToonGlobalOutlineWidth;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);

                float4 posCS   = TransformObjectToHClip(IN.positionOS.xyz);
                float3 normalVS = mul((float3x3)UNITY_MATRIX_IT_MV, IN.normalOS);

                // 스크린 스페이스 균일 두께: posCS.w 곱으로 원근에 따라 일정하게 보이도록
                float2 normalCS = normalize(float2(
                    UNITY_MATRIX_P[0][0] * normalVS.x,
                    UNITY_MATRIX_P[1][1] * normalVS.y));

                float width = _ToonGlobalOutlineWidth > 0.0001 ? _ToonGlobalOutlineWidth : _OutlineWidth;
                posCS.xy += normalCS * width * posCS.w;

                OUT.positionCS = posCS;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                return _OutlineColor;
            }
            ENDHLSL
        }
    }
}
