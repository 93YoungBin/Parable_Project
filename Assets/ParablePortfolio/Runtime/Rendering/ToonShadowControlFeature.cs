using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Parable.Rendering
{
    /// <summary>
    /// URPAsset의 Shadow/Cascade 값을 런타임으로 교체하는 Feature.
    /// URP 14에서 cascade split setter가 private이므로 Reflection으로 접근.
    ///
    /// 시행착오 메모:
    ///   - ScriptableRenderPass.Execute 타이밍에서 URPAsset을 수정하면
    ///     이미 Shadow Pass가 끝난 뒤라 효과 없음.
    ///   - RenderPipelineManager.beginCameraRendering 이 올바른 후킹 지점.
    ///   - per-frame Restore는 불필요: beginCameraRendering에서 매 프레임 Apply가
    ///     덮어쓰므로, Restore는 Dispose(씬 종료/Play Mode 종료) 시 1회만 수행.
    /// </summary>
    [Serializable]
    public class ToonShadowControlFeature : ScriptableRendererFeature
    {
        public ToonShadowSettings shadowSettings;

        // ── Reflection 캐시 (cascade split: internal setter, 외부 어셈블리 접근 불가) ──
        // shadowCascadeCount : public setter  → 직접 접근
        // cascade2/3/4Split  : internal setter → Reflection 필요
        static readonly FieldInfo s_Cascade2Split = typeof(UniversalRenderPipelineAsset)
            .GetField("m_Cascade2Split", BindingFlags.NonPublic | BindingFlags.Instance);
        static readonly FieldInfo s_Cascade3Split = typeof(UniversalRenderPipelineAsset)
            .GetField("m_Cascade3Split", BindingFlags.NonPublic | BindingFlags.Instance);
        static readonly FieldInfo s_Cascade4Split = typeof(UniversalRenderPipelineAsset)
            .GetField("m_Cascade4Split", BindingFlags.NonPublic | BindingFlags.Instance);

        // ── 원본값 스냅샷 (최초 1회) ─────────────────────────────────
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
        ShadowSnapshot _original;
        bool           _snapshotTaken;
        bool           _lastEnabled;

        // ── 카메라 필터링 캐시 (카메라당 1회만 GetComponent) ─────────
        readonly Dictionary<UnityEngine.Camera, bool> _camCache = new Dictionary<UnityEngine.Camera, bool>();

        UniversalRenderPipelineAsset _urpAsset;

        // ─────────────────────────────────────────────────────────────
        public override void Create()
        {
            _urpAsset = GraphicsSettings.defaultRenderPipeline as UniversalRenderPipelineAsset;

            RenderPipelineManager.beginCameraRendering += OnBeginCamera;
            // endCameraRendering 콜백 불필요: per-frame Restore 제거
        }

        protected override void Dispose(bool disposing)
        {
            RenderPipelineManager.beginCameraRendering -= OnBeginCamera;

            _camCache.Clear();

            // 씬 종료 / Play Mode 종료 / 앱 종료 시 원본값 1회 복원
            // → 에디터 Pipeline Asset 오염 방지
            if (_snapshotTaken && _urpAsset != null)
                Restore(_urpAsset, _original);
        }

        // ScriptableRenderPass는 이 Feature에서 사용하지 않음
        // (Shadow 설정은 Shadow Pass 실행 전에 주입해야 하므로 콜백 방식 사용)
        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData) { }

        // ─────────────────────────────────────────────────────────────
        void OnBeginCamera(ScriptableRenderContext ctx, UnityEngine.Camera cam)
        {
            if (_urpAsset == null) return;

            // Game 카메라만 처리 (SceneView / Preview / Reflection 등 제외)
            if (cam.cameraType != CameraType.Game) return;

            // UI 전용 카메라 제외 (UI 레이어만 렌더링하는 카메라)
            if (!_camCache.TryGetValue(cam, out bool allowed))
            {
                int uiOnly = 1 << LayerMask.NameToLayer("UI");
                allowed = cam.cullingMask != uiOnly;
                _camCache[cam] = allowed;
            }
            if (!allowed) return;

            bool enabled = shadowSettings != null && shadowSettings.enabled;

            // enabled가 꺼진 순간 즉시 원본값 복원
            if (_lastEnabled && !enabled)
            {
                if (_snapshotTaken) Restore(_urpAsset, _original);
                _lastEnabled = false;
                return;
            }

            if (!enabled) return;

            // 원본값 최초 1회만 스냅샷
            if (!_snapshotTaken)
            {
                _original      = Snapshot(_urpAsset);
                _snapshotTaken = true;
            }

            Apply(_urpAsset, shadowSettings);
            _lastEnabled = true;
        }

        // ── 헬퍼 ─────────────────────────────────────────────────────
        static ShadowSnapshot Snapshot(UniversalRenderPipelineAsset a) => new ShadowSnapshot
        {
            shadowDistance = a.shadowDistance,
            cascadeCount   = a.shadowCascadeCount,
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

            a.shadowCascadeCount = s.cascadeCount;

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
            if (snap.shadowDistance > 0f)
                a.shadowDistance = snap.shadowDistance;

            a.shadowDepthBias  = snap.depthBias;
            a.shadowNormalBias = snap.normalBias;

            // cascadeCount가 0이면 이전 Reflection 코드가 검증 우회해서 쓴 오염값
            // → 유효 범위(1~4)로 클램프
            int safeCount = Mathf.Clamp(snap.cascadeCount, 1, 4);
            a.shadowCascadeCount = safeCount;

            s_Cascade2Split?.SetValue(a, snap.cascade2Split);
            s_Cascade3Split?.SetValue(a, snap.cascade3Split);
            s_Cascade4Split?.SetValue(a, snap.cascade4Split);
        }
    }
}