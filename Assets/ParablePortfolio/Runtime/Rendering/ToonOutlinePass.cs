using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Parable.Rendering
{
    /// <summary>
    /// Layer-filtered outline pass.
    ///
    /// 렌더 큐: opaque
    /// 필터링:  Settings.outlineLayerMask 에 속한 렌더러만 추출
    /// 드로잉:  해당 렌더러들의 원본 머티리얼을 무시하고
    ///          Settings.outlineOverrideMaterial 로 re-draw
    ///
    /// 이 방식이 "Parable 스타일"의 핵심 —
    /// 오브젝트에 툰 머티리얼을 붙이지 않아도 레이어만 옮기면 아웃라인이 생긴다.
    ///
    /// 최적화:
    ///   - ShaderTagId 배열 static 보관 (프레임마다 alloc 방지)
    ///   - ProfilingSampler 캐싱
    ///   - layerMask / overrideMaterial null-check로 early-out
    ///   - FilteringSettings 은 struct, 값만 갱신하고 재할당 안 함
    /// </summary>
    public class ToonOutlinePass : ScriptableRenderPass
    {
        // URP가 DrawRenderers에 전달하기 위해 IList<ShaderTagId>를 요구함
        static readonly List<ShaderTagId> s_ShaderTags = new List<ShaderTagId>
        {
            new ShaderTagId("UniversalForward"),
            new ShaderTagId("SRPDefaultUnlit"),
            new ShaderTagId("LightweightForward"),
        };

        static readonly ProfilingSampler s_Sampler = new ProfilingSampler("Toon Outline (Layer)");

        readonly ToonRendererFeature.Settings _settings;
        FilteringSettings _filtering;

        public ToonOutlinePass(ToonRendererFeature.Settings settings)
        {
            _settings       = settings;
            renderPassEvent = settings.outlineEvent;

            // 아웃라인 패스: Avatar 레이어 + bit 8 동시 조건
            var layerMask = LayerMask.GetMask("Avatar");  // 아바타 고정 레이어
            _filtering = new FilteringSettings(RenderQueueRange.opaque, layerMask);
            _filtering.renderingLayerMask = 1u << 8;
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            // 런타임에 renderingLayerMask 변경을 즉시 반영
            _filtering.renderingLayerMask = _settings.outlineRenderingLayerMask;

            if (_settings.outlineOverrideMaterial == null || _settings.outlineRenderingLayerMask == 0)
                return;

            var cmd = CommandBufferPool.Get();
            using (new ProfilingScope(cmd, s_Sampler))
            {
                context.ExecuteCommandBuffer(cmd);
                cmd.Clear();

                var drawSettings = CreateDrawingSettings(
                    s_ShaderTags, ref renderingData, SortingCriteria.CommonOpaque);

                drawSettings.overrideMaterial          = _settings.outlineOverrideMaterial;
                drawSettings.overrideMaterialPassIndex = 0;

                context.DrawRenderers(
                    renderingData.cullResults, ref drawSettings, ref _filtering);
            }

            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }
    }
}
