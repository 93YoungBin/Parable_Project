using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Parable.Rendering
{
    /// <summary>
    /// URPAsset의 Shadow/Cascade 값을 카메라 렌더 전/후로 교체·복원하는 Feature.
    /// URP 14에서 cascade split setter가 private이므로 Reflection으로 접근.
    ///
    /// 시행착오 메모:
    ///   - ScriptableRenderPass.Execute 타이밍에서 URPAsset을 수정하면
    ///     이미 Shadow Pass가 끝난 뒤라 효과 없음.
    ///   - RenderPipelineManager.beginCameraRendering 이 올바른 후킹 지점.
    /// </summary>
    [Serializable]
    public class ToonShadowControlFeature : ScriptableRendererFeature
    {
        public ToonShadowSettings shadowSettings;

        // ── Reflection 캐시 (URPAsset private fields) ─────────────────
        static readonly FieldInfo s_CascadeCount  = typeof(UniversalRenderPipelineAsset)
            .GetField("m_ShadowCascadeCount",  BindingFlags.NonPublic | BindingFlags.Instance);
        static readonly FieldInfo s_Cascade2Split = typeof(UniversalRenderPipelineAsset)
            .GetField("m_Cascade2Split",        BindingFlags.NonPublic | BindingFlags.Instance);
        static readonly FieldInfo s_Cascade3Split = typeof(UniversalRenderPipelineAsset)
            .GetField("m_Cascade3Split",        BindingFlags.NonPublic | BindingFlags.Instance);
        static readonly FieldInfo s_Cascade4Split = typeof(UniversalRenderPipelineAsset)
            .GetField("m_Cascade4Split",        BindingFlags.NonPublic | BindingFlags.Instance);

        // ── 복원용 스냅샷 ────────────────────────────────────────────
        struct ShadowSnapshot
        {
            public float   shadowDistance;
            public int     cascadeCount;
            public float   cascade2Split;
            public Vector2 cascade3Split;
            public Vector3 cascade4Split;
            public float   depthBias;
            public float   normalBias;
        }
        ShadowSnapshot _saved;

        UniversalRenderPipelineAsset _urpAsset;

        // ─────────────────────────────────────────────────────────────
        public override void Create()
        {
            _urpAsset = GraphicsSettings.defaultRenderPipeline as UniversalRenderPipelineAsset;

            RenderPipelineManager.beginCameraRendering += OnBeginCamera;
            RenderPipelineManager.endCameraRendering   += OnEndCamera;
        }

        protected override void Dispose(bool disposing)
        {
            RenderPipelineManager.beginCameraRendering -= OnBeginCamera;
            RenderPipelineManager.endCameraRendering   -= OnEndCamera;
        }

        // ScriptableRenderPass는 이 Feature에서 사용하지 않음
        // (Shadow 설정은 Shadow Pass 실행 전에 주입해야 하므로 콜백 방식 사용)
        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData) { }

        // ─────────────────────────────────────────────────────────────
        void OnBeginCamera(ScriptableRenderContext ctx, Camera cam)
        {
            if (shadowSettings == null || !shadowSettings.enabled || _urpAsset == null)
                return;

            // 현재값 스냅샷
            _saved = Snapshot(_urpAsset);

            // 오버라이드 적용
            Apply(_urpAsset, shadowSettings);
        }

        void OnEndCamera(ScriptableRenderContext ctx, Camera cam)
        {
            if (_urpAsset == null) return;
            Restore(_urpAsset, _saved);
        }

        // ── 헬퍼 ─────────────────────────────────────────────────────
        static ShadowSnapshot Snapshot(UniversalRenderPipelineAsset a) => new ShadowSnapshot
        {
            shadowDistance = a.shadowDistance,
            cascadeCount   = (int)(s_CascadeCount?.GetValue(a) ?? 1),
            cascade2Split  = (float)(s_Cascade2Split?.GetValue(a) ?? 0.25f),
            cascade3Split  = (Vector2)(s_Cascade3Split?.GetValue(a) ?? Vector2.zero),
            cascade4Split  = (Vector3)(s_Cascade4Split?.GetValue(a) ?? Vector3.zero),
            depthBias      = a.shadowDepthBias,
            normalBias     = a.shadowNormalBias,
        };

        static void Apply(UniversalRenderPipelineAsset a, ToonShadowSettings s)
        {
            a.shadowDistance   = s.shadowDistance;
            a.shadowDepthBias  = s.depthBias;
            a.shadowNormalBias = s.normalBias;

            s_CascadeCount?.SetValue(a, s.cascadeCount);

            switch (s.cascadeCount)
            {
                case 2:
                    s_Cascade2Split?.SetValue(a, Mathf.Clamp01(s.split1));
                    break;
                case 3:
                    s_Cascade3Split?.SetValue(a,
                        new Vector2(Mathf.Clamp01(s.split1),
                                    Mathf.Clamp01(Mathf.Max(s.split1 + 0.01f, s.split2))));
                    break;
                case 4:
                    float v1 = Mathf.Clamp01(s.split1);
                    float v2 = Mathf.Clamp01(Mathf.Max(v1 + 0.01f, s.split2));
                    float v3 = Mathf.Clamp01(Mathf.Max(v2 + 0.01f, s.split3));
                    s_Cascade4Split?.SetValue(a, new Vector3(v1, v2, v3));
                    break;
            }
        }

        static void Restore(UniversalRenderPipelineAsset a, ShadowSnapshot snap)
        {
            a.shadowDistance   = snap.shadowDistance;
            a.shadowDepthBias  = snap.depthBias;
            a.shadowNormalBias = snap.normalBias;

            s_CascadeCount?.SetValue(a,  snap.cascadeCount);
            s_Cascade2Split?.SetValue(a, snap.cascade2Split);
            s_Cascade3Split?.SetValue(a, snap.cascade3Split);
            s_Cascade4Split?.SetValue(a, snap.cascade4Split);
        }
    }
}