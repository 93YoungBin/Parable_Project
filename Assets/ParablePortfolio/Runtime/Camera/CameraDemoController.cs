using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Parable.Camera
{
    /// <summary>
    /// Phase 3 카메라 데모 UI 컨트롤러.
    ///
    /// 기능:
    ///   - 카메라 슬롯 버튼 동적 생성 (CameraDirector.cameras 기반)
    ///   - 활성 카메라 라벨 표시
    ///   - Blend Duration 슬라이더 (런타임 핫스왑)
    ///   - Noise Amplitude 슬라이더 (핸드헬드 강도)
    /// </summary>
    public class CameraDemoController : MonoBehaviour
    {
        [Header("References")]
        public CameraDirector director;

        [Header("UI")]
        public Transform      btnContainer;
        public Button         btnPrefab;
        public Text           lblActiveCam;
        public Slider         sliderBlendDuration;
        public Slider         sliderNoise;

        readonly List<Button> _camButtons = new List<Button>();

        void Start()
        {
            if (director == null) return;

            BuildCameraButtons();

            director.OnCameraChanged += OnCameraChanged;

            if (sliderBlendDuration)
            {
                sliderBlendDuration.minValue = 0f;
                sliderBlendDuration.maxValue = 3f;
                sliderBlendDuration.value    = 0.5f;
                sliderBlendDuration.onValueChanged.AddListener(OnBlendDurationChanged);
            }

            if (sliderNoise)
            {
                sliderNoise.minValue = 0f;
                sliderNoise.maxValue = 3f;
                sliderNoise.value    = 0f;
                sliderNoise.onValueChanged.AddListener(OnNoiseChanged);
            }

            // 초기 라벨 갱신
            RefreshLabel(director.ActiveIndex);
        }

        void OnDestroy()
        {
            if (director != null) director.OnCameraChanged -= OnCameraChanged;
        }

        void BuildCameraButtons()
        {
            if (btnPrefab == null || btnContainer == null) return;

            for (int i = 0; i < director.cameras.Count; i++)
            {
                int idx   = i;
                var btn   = Instantiate(btnPrefab, btnContainer);
                var label = btn.GetComponentInChildren<Text>();
                if (label) label.text = string.IsNullOrEmpty(director.cameras[i].label)
                    ? $"CAM {i + 1}"
                    : director.cameras[i].label;

                btn.onClick.AddListener(() => director.SwitchTo(idx));
                _camButtons.Add(btn);
            }
        }

        void OnCameraChanged(int index, CameraDirector.CameraSlot slot)
        {
            RefreshLabel(index);
            HighlightButton(index);
        }

        void RefreshLabel(int index)
        {
            if (lblActiveCam == null) return;
            if (index < 0 || index >= director.cameras.Count)
            {
                lblActiveCam.text = "---";
                return;
            }
            var slot = director.cameras[index];
            lblActiveCam.text = string.IsNullOrEmpty(slot.label) ? $"CAM {index + 1}" : slot.label;
        }

        void HighlightButton(int activeIndex)
        {
            for (int i = 0; i < _camButtons.Count; i++)
            {
                var colors = _camButtons[i].colors;
                colors.normalColor = (i == activeIndex)
                    ? new Color(0.4f, 0.8f, 1f)
                    : Color.white;
                _camButtons[i].colors = colors;
            }
        }

        // ── 슬라이더 콜백 ─────────────────────────────────────────────

        void OnBlendDurationChanged(float v)
        {
            int idx = director.ActiveIndex;
            if (idx < 0 || idx >= director.cameras.Count) return;
            var profile = director.cameras[idx].profile;
            if (profile != null) profile.blendDuration = v;
        }

        void OnNoiseChanged(float v)
        {
            int idx = director.ActiveIndex;
            if (idx < 0 || idx >= director.cameras.Count) return;
            var profile = director.cameras[idx].profile;
            if (profile == null) return;
            profile.noiseAmplitude = v;
            director.ApplyProfileToCurrent(profile);
        }
    }
}
