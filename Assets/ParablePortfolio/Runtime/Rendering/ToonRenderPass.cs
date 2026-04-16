using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Parable.Rendering
{
    /// <summary>
    /// ToonLit 셰이더에 글로벌 파라미터를 주입하는 렌더 패스.
    /// Feature의 Settings 값을 씬 전체 오브젝트에 일괄 적용.
    /// T-1.3: ShadowMap 커스터마이즈 연동 예정
    /// T-1.4: Post-Processing 연동 예정
    /// </summary>
    public class ToonRenderPass : ScriptableRenderPass
    {
        static readonly int s_RampThreshold = Shader.PropertyToID("_ToonGlobalRampThreshold");
        static readonly int s_RampSmooth    = Shader.PropertyToID("_ToonGlobalRampSmooth");
        static readonly int s_OutlineWidth  = Shader.PropertyToID("_ToonGlobalOutlineWidth");

        private readonly ToonRendererFeature.Settings _settings;
        private readonly ProfilingSampler _sampler;

        public ToonRenderPass(ToonRendererFeature.Settings settings)
        {
            _settings = settings;
            _sampler  = new ProfilingSampler("ToonRenderPass");
            renderPassEvent = settings.renderPassEvent;
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            if (!_settings.isEnabled) return;

            var cmd = CommandBufferPool.Get();
            using (new ProfilingScope(cmd, _sampler))
            {
                if (_settings.overrideGlobalRamp)
                {
                    cmd.SetGlobalFloat(s_RampThreshold, _settings.globalRampThreshold);
                    cmd.SetGlobalFloat(s_RampSmooth,    _settings.globalRampSmooth);
                }
                if (_settings.overrideGlobalOutline)
                    cmd.SetGlobalFloat(s_OutlineWidth, _settings.globalOutlineWidth);
            }

            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }
    }
}