using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Parable.Rendering
{
    /// <summary>
    /// URP Volume 파라미터를 ScriptableRendererFeature에서 런타임 제어.
    ///
    /// 구조:
    ///   씬의 Global Volume ──▶ Volume Profile
    ///                               ├─ Bloom
    ///                               ├─ ColorAdjustments
    ///                               ├─ Tonemapping
    ///                               └─ DepthOfField
    ///   ToonPostProcessFeature.Execute() 에서 위 컴포넌트 파라미터를
    ///   ToonPostProcessSettings 값으로 오버라이드.
    ///
    /// 시행착오 메모:
    ///   - VolumeComponent.Override() 는 IsOverridden=true 설정까지 포함.
    ///   - Volume.profile 직접 수정 시 에셋 영구 변경됨 →
    ///     profile.TryGet 후 parameter.Override(value) 사용.
    /// </summary>
    [Serializable]
    public class ToonPostProcessFeature : ScriptableRendererFeature
    {
        public ToonPostProcessSettings settings;

        private ToonPostProcessPass _pass;

        public override void Create()
        {
            _pass = new ToonPostProcessPass(settings);
            _pass.renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing;
        }

        public override void AddRenderPasses(ScriptableRenderer renderer,
                                             ref RenderingData renderingData)
        {
            if (settings == null || !settings.enabled) return;
            renderer.EnqueuePass(_pass);
        }
    }

    // ─────────────────────────────────────────────────────────────────
    public class ToonPostProcessPass : ScriptableRenderPass
    {
        private readonly ToonPostProcessSettings _s;
        private readonly ProfilingSampler _sampler =
            new ProfilingSampler("ToonPostProcess");

        public ToonPostProcessPass(ToonPostProcessSettings s) { _s = s; }

        public override void Execute(ScriptableRenderContext context,
                                     ref RenderingData renderingData)
        {
            if (_s == null || !_s.enabled) return;

            // Global Volume 스택에서 컴포넌트 취득
            var stack = VolumeManager.instance.stack;

            ApplyBloom(stack.GetComponent<Bloom>());
            ApplyColorAdjustments(stack.GetComponent<ColorAdjustments>());
            ApplyTonemapping(stack.GetComponent<Tonemapping>());
            ApplyDOF(stack.GetComponent<DepthOfField>());
        }

        void ApplyBloom(Bloom b)
        {
            if (b == null) return;
            b.active = _s.bloomEnabled;
            if (!_s.bloomEnabled) return;
            b.threshold.Override(_s.bloomThreshold);
            b.intensity.Override(_s.bloomIntensity);
            b.scatter.Override(_s.bloomScatter);
            b.tint.Override(_s.bloomTint);
        }

        void ApplyColorAdjustments(ColorAdjustments ca)
        {
            if (ca == null) return;
            ca.active = _s.colorEnabled;
            if (!_s.colorEnabled) return;
            ca.postExposure.Override(_s.postExposure);
            ca.contrast.Override(_s.contrast);
            ca.saturation.Override(_s.saturation);
            ca.hueShift.Override(_s.hueShift);
        }

        void ApplyTonemapping(Tonemapping tm)
        {
            if (tm == null) return;
            tm.mode.Override(_s.tonemapping);
        }

        void ApplyDOF(DepthOfField dof)
        {
            if (dof == null) return;
            dof.active = _s.dofEnabled;
            if (!_s.dofEnabled) return;
            dof.mode.Override(_s.dofMode);
            dof.focusDistance.Override(_s.focusDistance);
            dof.aperture.Override(_s.aperture);
            dof.focalLength.Override(_s.focalLength);
        }
    }
}