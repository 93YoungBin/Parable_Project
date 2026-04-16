using System;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Parable.Rendering
{
    /// <summary>
    /// URP ScriptableRendererFeature — Toon 렌더링 파이프라인의 진입점.
    /// URP Renderer Data 에셋(Parable_URPRenderer)에 등록되어 있음.
    /// </summary>
    [Serializable]
    public class ToonRendererFeature : ScriptableRendererFeature
    {
        [Serializable]
        public class Settings
        {
            public bool isEnabled = true;
            public RenderPassEvent renderPassEvent = RenderPassEvent.BeforeRenderingOpaques;

            [Header("Global Ramp Override")]
            [Tooltip("ON이면 머티리얼 설정을 무시하고 아래 값을 씬 전체에 적용")]
            public bool  overrideGlobalRamp   = false;
            [Range(0f, 1f)]   public float globalRampThreshold = 0.5f;
            [Range(0f, 0.2f)] public float globalRampSmooth    = 0.04f;

            [Header("Global Outline Override")]
            public bool  overrideGlobalOutline  = false;
            [Range(0f, 0.05f)] public float globalOutlineWidth = 0.005f;
        }

        public Settings settings = new Settings();
        private ToonRenderPass _pass;

        public override void Create()
        {
            _pass = new ToonRenderPass(settings);
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (!settings.isEnabled) return;
            renderer.EnqueuePass(_pass);
        }
    }
}