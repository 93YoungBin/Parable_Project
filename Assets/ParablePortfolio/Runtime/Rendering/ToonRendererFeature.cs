using System;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Parable.Rendering
{
    /// <summary>
    /// URP ScriptableRendererFeature — 툰 렌더링 파이프라인의 진입점.
    ///
    /// 구조 (리팩토링 후):
    ///   [1] ToonGlobalParamsPass  — 글로벌 셰이더 파라미터 주입 (비용 무시)
    ///   [2] ToonOutlinePass       — 레이어 필터 + DrawRenderers 로 override drawing
    ///
    /// 레이어 기반 렌더링 의의:
    ///   - 오브젝트는 원본 머티리얼(StandardLit, ToonLit 등 무관)을 유지
    ///   - 레이어만 Toon 으로 이동하면 RendererFeature 가 감지
    ///   - outlineOverrideMaterial 로 해당 렌더러들을 재드로잉 → 외곽선 추가
    ///   - 런타임 토글: gameObject.layer 값 하나만 바꾸면 됨
    /// </summary>
    [Serializable]
    public class ToonRendererFeature : ScriptableRendererFeature
    {
        [Serializable]
        public class Settings
        {
            public bool isEnabled = true;

            [Header("Outline (RenderingLayer 기반 Override)")]
            [Tooltip("이 renderingLayerMask 비트를 가진 Renderer만 outlineOverrideMaterial 로 재드로잉. 기본값 256 = bit 8.")]
            public uint outlineRenderingLayerMask = 1u << 8;
            [Tooltip("Parable/ToonOutlineReplace 셰이더를 쓰는 머티리얼을 할당")]
            public Material  outlineOverrideMaterial;
            public RenderPassEvent outlineEvent = RenderPassEvent.AfterRenderingOpaques;

            [Header("Global Ramp Override (모든 ToonLit 머티리얼에 적용)")]
            public bool  overrideGlobalRamp = false;
            [Range(0f, 1f)]   public float globalRampThreshold = 0.5f;
            [Range(0f, 0.2f)] public float globalRampSmooth    = 0.04f;

            [Header("Global Outline Override")]
            public bool  overrideGlobalOutline = false;
            [Range(0f, 0.05f)] public float globalOutlineWidth = 0.005f;

            [Header("Global Shadow Color Override")]
            public bool  overrideShadowColor = false;
            public Color shadowColor = new Color(0.25f, 0.28f, 0.4f, 1f);
        }

        public Settings settings = new Settings();

        ToonGlobalParamsPass _globalPass;
        ToonOutlinePass      _outlinePass;

        public override void Create()
        {
            _globalPass  = new ToonGlobalParamsPass(settings);
            _outlinePass = new ToonOutlinePass(settings);
        }

        public override void AddRenderPasses(ScriptableRenderer renderer,
                                             ref RenderingData renderingData)
        {
            if (!settings.isEnabled) return;

            // 글로벌 파라미터 주입은 항상 수행 (비용 무시)
            renderer.EnqueuePass(_globalPass);

            // 아웃라인은 override material + layer mask 가 모두 세팅되어야 실행
            if (settings.outlineOverrideMaterial != null && settings.outlineRenderingLayerMask != 0)
                renderer.EnqueuePass(_outlinePass);
        }
    }
}
