using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Rendering.Universal;

namespace Parable.Rendering
{
    public class ToonDemoController : MonoBehaviour
    {
        [Header("Target Renderers")]
        public Renderer[] targets;

        [Header("Toon Material (교체용)")]
        public Material toonMaterial;

        [Header("Outline RenderingLayer")]
        [Tooltip("Renderer.renderingLayerMask 에 설정할 비트. Feature의 outlineRenderingLayerMask 와 일치해야 함. 기본값 256 = bit 8.")]
        public uint outlineRenderingLayerBit = 1u << 8;

        [Header("Buttons")]
        public Button btnOutlineToggle;
        public Button btnRampHard;
        public Button btnRampSoft;
        public Button btnShadowToggle;
        public Button btnColorCycle;
        public Button btnShaderSwap;

        [Header("Sliders")]
        public Slider sliderOutlineWidth;
        public Slider sliderRampThreshold;

        static readonly int s_RampThreshold = Shader.PropertyToID("_RampThreshold");
        static readonly int s_RampSmooth    = Shader.PropertyToID("_RampSmooth");
        static readonly int s_BaseColor     = Shader.PropertyToID("_BaseColor");
        static readonly int s_ShadowColor   = Shader.PropertyToID("_ShadowColor");

        bool _outlineOn = true;
        bool _featureOn = true;
        bool _isToon    = true;
        int  _colorIndex = 0;

        Material[][] _originalMats;

        static readonly Color[] s_Palettes = {
            new Color(0.90f, 0.55f, 0.30f),
            new Color(0.25f, 0.45f, 0.85f),
            new Color(0.35f, 0.75f, 0.45f),
            new Color(0.80f, 0.30f, 0.45f),
            new Color(0.85f, 0.82f, 0.78f),
        };
        static readonly Color[] s_ShadowPalettes = {
            new Color(0.30f, 0.18f, 0.28f),
            new Color(0.10f, 0.15f, 0.40f),
            new Color(0.12f, 0.28f, 0.18f),
            new Color(0.28f, 0.10f, 0.15f),
            new Color(0.22f, 0.20f, 0.30f),
        };

        void Start()
        {
            _originalMats = new Material[targets.Length][];
            for (int i = 0; i < targets.Length; i++)
            {
                if (targets[i] == null) continue;
                _originalMats[i] = targets[i].sharedMaterials;
                // 초기 상태: outlineOn = true — renderingLayerMask 비트 ON
                targets[i].renderingLayerMask |= outlineRenderingLayerBit;
            }

            if (btnOutlineToggle) btnOutlineToggle.onClick.AddListener(ToggleOutline);
            if (btnRampHard)      btnRampHard.onClick.AddListener(SetRampHard);
            if (btnRampSoft)      btnRampSoft.onClick.AddListener(SetRampSoft);
            if (btnShadowToggle)  btnShadowToggle.onClick.AddListener(ToggleFeature);
            if (btnColorCycle)    btnColorCycle.onClick.AddListener(CycleColor);
            if (btnShaderSwap)    btnShaderSwap.onClick.AddListener(ToggleShader);

            if (sliderOutlineWidth)
            {
                sliderOutlineWidth.minValue = 0f;
                sliderOutlineWidth.maxValue = 0.03f;
                sliderOutlineWidth.value    = 0.005f;
                sliderOutlineWidth.onValueChanged.AddListener(SetOutlineWidth);
                SetOutlineWidth(sliderOutlineWidth.value);
            }
            if (sliderRampThreshold)
            {
                sliderRampThreshold.minValue = 0f;
                sliderRampThreshold.maxValue = 1f;
                sliderRampThreshold.value    = 0.45f;
                sliderRampThreshold.onValueChanged.AddListener(SetRampThreshold);
            }
        }

        // ── Outline: renderingLayerMask 비트 ON/OFF ──────────────────
        void ToggleOutline()
        {
            _outlineOn = !_outlineOn;
            foreach (var r in targets)
            {
                if (r == null) continue;
                if (_outlineOn) r.renderingLayerMask |=  outlineRenderingLayerBit;
                else            r.renderingLayerMask &= ~outlineRenderingLayerBit;
            }

            if (btnOutlineToggle)
            {
                var txt = btnOutlineToggle.GetComponentInChildren<Text>();
                if (txt) txt.text = _outlineOn ? "Outline: ON" : "Outline: OFF";
            }
        }

        // ── Outline 두께: Feature 글로벌 파라미터 ────────────────────
        void SetOutlineWidth(float v)
        {
            var feat = FindToonFeature();
            if (feat == null) return;
            feat.settings.overrideGlobalOutline = v > 0f;
            feat.settings.globalOutlineWidth    = v;
        }

        // ── Ramp / Color: ToonLit 머티리얼 직접 조작 ─────────────────
        void SetAllFloat(int id, float val)
        {
            foreach (var r in targets)
                if (r != null) r.material.SetFloat(id, val);
        }

        void SetAllColor(int id, Color col)
        {
            foreach (var r in targets)
                if (r != null) r.material.SetColor(id, col);
        }

        void SetRampHard()             { SetAllFloat(s_RampSmooth, 0.01f); }
        void SetRampSoft()             { SetAllFloat(s_RampSmooth, 0.12f); }
        void SetRampThreshold(float v) { SetAllFloat(s_RampThreshold, v); }

        void ToggleFeature()
        {
            _featureOn = !_featureOn;
            var feat = FindToonFeature();
            if (feat != null) feat.settings.isEnabled = _featureOn;
            if (btnShadowToggle)
            {
                var txt = btnShadowToggle.GetComponentInChildren<Text>();
                if (txt) txt.text = _featureOn ? "ToonPass: ON" : "ToonPass: OFF";
            }
        }

        void CycleColor()
        {
            _colorIndex = (_colorIndex + 1) % s_Palettes.Length;
            SetAllColor(s_BaseColor,   s_Palettes[_colorIndex]);
            SetAllColor(s_ShadowColor, s_ShadowPalettes[_colorIndex]);
        }

        // ── 셰이더 스왑 (PBR ↔ ToonLit) ─────────────────────────────
        void ToggleShader()
        {
            _isToon = !_isToon;
            for (int i = 0; i < targets.Length; i++)
            {
                if (targets[i] == null) continue;
                if (_isToon && toonMaterial != null)
                {
                    var mats = new Material[targets[i].sharedMaterials.Length];
                    for (int m = 0; m < mats.Length; m++) mats[m] = toonMaterial;
                    targets[i].materials = mats;
                }
                else if (!_isToon && _originalMats[i] != null)
                {
                    targets[i].materials = _originalMats[i];
                }
            }
            if (btnShaderSwap)
            {
                var txt = btnShaderSwap.GetComponentInChildren<Text>();
                if (txt) txt.text = _isToon ? "→ Original" : "→ ToonLit";
            }
        }

        static ToonRendererFeature FindToonFeature()
        {
            var urp = UnityEngine.Rendering.GraphicsSettings.defaultRenderPipeline
                as UniversalRenderPipelineAsset;
            if (urp == null) return null;

            var f = typeof(UniversalRenderPipelineAsset).GetField("m_RendererDataList",
                System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.Instance);
            var renderers = f?.GetValue(urp) as ScriptableRendererData[];
            if (renderers == null) return null;

            foreach (var rd in renderers)
                if (rd != null)
                    foreach (var feat in rd.rendererFeatures)
                        if (feat is ToonRendererFeature tf) return tf;
            return null;
        }
    }
}
