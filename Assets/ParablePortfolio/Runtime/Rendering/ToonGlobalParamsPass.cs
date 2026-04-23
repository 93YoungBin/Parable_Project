using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Parable.Rendering
{
    /// <summary>
    /// 글로벌 툰 셰이더 파라미터를 주입하는 경량 패스.
    ///
    /// 역할:
    ///   - Ramp Threshold / Smooth 전역 오버라이드
    ///   - Outline Width 전역 오버라이드 (ToonOutlineReplace.shader 가 읽음)
    ///   - Shadow Color 전역 오버라이드 (ToonLit.shader 가 읽음)
    ///
    /// 이 패스는 렌더 타겟을 건드리지 않음 —
    /// Shader.SetGlobalFloat / SetGlobalVector 만 수행하므로 비용 무시 가능.
    ///
    /// 과거 ToonRenderPass 에서 이름 변경: 역할을 "글로벌 파라미터 주입"으로 명확화.
    /// </summary>
    public class ToonGlobalParamsPass : ScriptableRenderPass
    {
        static readonly int s_RampThreshold  = Shader.PropertyToID("_ToonGlobalRampThreshold");
        static readonly int s_RampSmooth     = Shader.PropertyToID("_ToonGlobalRampSmooth");
        static readonly int s_OutlineWidth   = Shader.PropertyToID("_ToonGlobalOutlineWidth");
        static readonly int s_ShadowColor    = Shader.PropertyToID("_ToonGlobalShadowColor");

        static readonly ProfilingSampler s_Sampler = new ProfilingSampler("Toon Global Params");

        readonly ToonRendererFeature.Settings _settings;

        public ToonGlobalParamsPass(ToonRendererFeature.Settings settings)
        {
            _settings       = settings;
            renderPassEvent = RenderPassEvent.BeforeRenderingOpaques;
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            var cmd = CommandBufferPool.Get();
            using (new ProfilingScope(cmd, s_Sampler))
            {
                if (_settings.overrideGlobalRamp)
                {
                    cmd.SetGlobalFloat(s_RampThreshold, _settings.globalRampThreshold);
                    cmd.SetGlobalFloat(s_RampSmooth,    _settings.globalRampSmooth);
                }

                // outline width 는 override 가 꺼져있어도 셰이더가 "> 0" 체크로 분기하므로
                // 명시적으로 0 주입해 머티리얼 값이 살아나도록 함
                cmd.SetGlobalFloat(s_OutlineWidth,
                    _settings.overrideGlobalOutline ? _settings.globalOutlineWidth : 0f);

                if (_settings.overrideShadowColor)
                    cmd.SetGlobalColor(s_ShadowColor, _settings.shadowColor);
            }

            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }
    }
}
