using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Parable.Rendering
{
    /// <summary>
    /// Toon 렌더링에 최적화된 Post-Processing 파라미터 컨테이너.
    /// ToonPostProcessFeature가 Volume에 런타임으로 주입.
    ///
    /// 툰 특화 세팅:
    ///   - Bloom: 낮은 임계값 + 부드러운 Knee → 애니메이션 발광 느낌
    ///   - Color Adjustments: 채도 강조, 대비 낮춤 → 셀 애니 팔레트
    ///   - Tonemapping: ACES (시네마틱) 또는 None (컬러 보존)
    ///   - DOF: Bokeh 모드, 캐릭터 초점 거리 기준
    /// </summary>
    [CreateAssetMenu(fileName = "ToonPostProcessSettings",
                     menuName  = "Parable/Toon PostProcess Settings")]
    public class ToonPostProcessSettings : ScriptableObject
    {
        public bool enabled = true;

        [Header("Bloom")]
        public bool  bloomEnabled    = true;
        [Range(0f, 1f)]   public float bloomThreshold  = 0.85f;
        [Range(0f, 1f)]   public float bloomIntensity  = 0.4f;
        [Range(0f, 1f)]   public float bloomScatter    = 0.7f;
        public Color bloomTint = new Color(1f, 0.95f, 0.85f);

        [Header("Color Adjustments")]
        public bool  colorEnabled      = true;
        [Range(-100f, 100f)] public float postExposure    = 0f;
        [Range(-100f, 100f)] public float contrast        = 10f;
        [Range(-100f, 100f)] public float saturation      = 20f;
        [Range(-180f, 180f)] public float hueShift        = 0f;

        [Header("Tonemapping")]
        public TonemappingMode tonemapping = TonemappingMode.None;

        [Header("Depth of Field")]
        public bool  dofEnabled      = true;
        public DepthOfFieldMode dofMode = DepthOfFieldMode.Bokeh;
        [Range(0.1f, 100f)] public float focusDistance  = 3f;
        [Range(1f,   32f)]  public float aperture       = 5.6f;
        [Range(1f,   300f)] public float focalLength    = 50f;
    }
}