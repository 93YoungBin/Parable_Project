using System;
using System.Collections.Generic;
using UnityEngine;
using Unity.Cinemachine;
using Parable.Rendering;

namespace Parable.Camera
{
    /// <summary>
    /// 다중 CinemachineCamera 실시간 연출 디렉터.
    ///
    /// Parable 아키텍처 포인트:
    ///   - 연출자가 런타임에 카메라 슬롯을 전환 → Brain 이 블렌드 처리
    ///   - CameraParamProfile 을 카메라에 주입해 "물리 기반 파라미터" 실시간 적용
    ///   - TriggerCut(int) 은 AnimationEvent 에서 직접 호출 가능
    ///     → 퍼포머 동작 키프레임과 카메라 컷 타이밍 동기화
    ///
    /// 전환 방식: CinemachineCamera.enabled ON/OFF (Priority 방식 대비 명확한 상태 관리)
    /// </summary>
    public class CameraDirector : MonoBehaviour
    {
        [Serializable]
        public class CameraSlot
        {
            public string             label;
            public CinemachineCamera  virtualCamera;
            public CameraParamProfile profile;
        }

        [Header("Brain")]
        public CinemachineBrain brain;

        [Header("Camera Slots")]
        public List<CameraSlot> cameras = new List<CameraSlot>();

        [Header("Shadow 연동")]
        [Tooltip("카메라 전환 시 Shadow 설정을 함께 교체할 ToonShadowSettings 에셋")]
        public ToonShadowSettings shadowSettings;

        [Header("Timeline 연동")]
        [Tooltip("true: CinemachineTrack이 VC 활성화를 담당. Start()가 VC enabled를 건드리지 않음.")]
        public bool managedByTimeline = false;

        public event Action<int, CameraSlot> OnCameraChanged;

        int _activeIndex = -1;
        public int ActiveIndex => _activeIndex;

        void Start()
        {
            if (managedByTimeline)
            {
                // CinemachineTrack이 VC 제어를 담당 — 모든 VC를 활성화하고 Brain에 위임
                foreach (var slot in cameras)
                    if (slot.virtualCamera != null) slot.virtualCamera.enabled = true;

                // 프로파일은 적용하되 _activeIndex 는 -1 (Timeline이 샷 결정)
                if (cameras.Count > 0 && cameras[0].profile != null)
                    ApplyShadow(cameras[0].profile);
                return;
            }

            // 수동 모드: 첫 번째 카메라만 활성화, 나머지 비활성화 + 프로파일 초기 적용
            for (int i = 0; i < cameras.Count; i++)
            {
                var vc = cameras[i].virtualCamera;
                if (vc != null) vc.enabled = (i == 0);
            }

            if (cameras.Count > 0)
            {
                _activeIndex = 0;
                var first = cameras[0];
                if (first.virtualCamera != null && first.profile != null)
                {
                    if (brain != null)
                        brain.DefaultBlend = ToBlendDefinition(first.profile);
                    ApplyProfile(first.virtualCamera, first.profile);
                    ApplyShadow(first.profile);
                }
            }
        }

        /// <summary>
        /// 카메라 전환. index 범위 밖이면 무시.
        /// AnimationEvent 에서 직접 호출 가능 (int 파라미터 1개).
        /// </summary>
        /// <param name="index">전환할 슬롯 인덱스</param>
        /// <param name="forceCut">true면 프로파일 블렌드 무시, 즉시 컷</param>
        public void SwitchTo(int index, bool forceCut = false)
        {
            if (index < 0 || index >= cameras.Count) return;
            if (index == _activeIndex) return;

            var next = cameras[index];
            if (next.virtualCamera == null) return;

            // 블렌드 설정 — forceCut이면 무조건 즉시 컷
            if (brain != null)
            {
                brain.DefaultBlend = forceCut
                    ? new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.Cut, 0f)
                    : (next.profile != null
                        ? ToBlendDefinition(next.profile)
                        : brain.DefaultBlend);
            }

            // 전환: 이전 OFF, 다음 ON
            if (_activeIndex >= 0 && cameras[_activeIndex].virtualCamera != null)
                cameras[_activeIndex].virtualCamera.enabled = false;

            next.virtualCamera.enabled = true;
            ApplyProfile(next.virtualCamera, next.profile);
            ApplyShadow(next.profile);

            _activeIndex = index;
            OnCameraChanged?.Invoke(_activeIndex, next);
        }

        /// <summary>AnimationEvent 용 alias.</summary>
        public void TriggerCut(int index) => SwitchTo(index);

        /// <summary>
        /// 현재 활성 카메라에 프로필을 핫스왑.
        /// Inspector 에서 실시간 파라미터 조정 시 사용.
        /// </summary>
        public void ApplyProfileToCurrent(CameraParamProfile profile)
        {
            if (_activeIndex < 0 || _activeIndex >= cameras.Count) return;
            var vc = cameras[_activeIndex].virtualCamera;
            if (vc != null) ApplyProfile(vc, profile);
        }

        // ── 프로필 주입 ───────────────────────────────────────────────

        static void ApplyProfile(CinemachineCamera vc, CameraParamProfile p)
        {
            if (p == null) return;

            // ── Physical Camera Lens ─────────────────────────────────
            // focalLength(mm) + sensorSize → FOV 역산 후 Physical 모드로 주입
            var lens = vc.Lens;
            lens.ModeOverride = LensSettings.OverrideModes.Physical;
            lens.FieldOfView  = p.ComputedFOV();   // Physical 모드에서도 Brain 내부 계산에 사용
            lens.NearClipPlane = p.nearClipPlane;
            lens.FarClipPlane  = p.farClipPlane;

            // PhysicalSettings struct — 반드시 통째로 재대입
            var phys = lens.PhysicalProperties;
            phys.SensorSize    = p.sensorSize;
            phys.Aperture      = p.aperture;
            phys.FocusDistance = p.focusDistance;
            phys.Iso           = p.iso;
            phys.ShutterSpeed  = p.shutterSpeed;
            lens.PhysicalProperties = phys;

            vc.Lens = lens;

            // Follow spring damping
            var follow = vc.GetComponent<CinemachineFollow>();
            if (follow != null)
            {
                var ts = follow.TrackerSettings;
                ts.PositionDamping = new Vector3(p.followDampingX, p.followDampingY, p.followDampingZ);
                follow.TrackerSettings = ts;
            }

            // Aim damping (Vector2 struct → 반드시 통째로 재대입)
            var aim = vc.GetComponent<CinemachineRotationComposer>();
            if (aim != null)
                aim.Damping = new Vector2(p.aimDampingH, p.aimDampingV);

            // Noise
            var noise = vc.GetComponent<CinemachineBasicMultiChannelPerlin>();
            if (noise != null)
            {
                noise.AmplitudeGain  = p.noiseAmplitude;
                noise.FrequencyGain = p.noiseFrequency;
            }
        }

        // ── Shadow 연동 ───────────────────────────────────────────────

        void ApplyShadow(CameraParamProfile p)
        {
            if (shadowSettings == null || p == null || !p.overrideShadow) return;

            shadowSettings.shadowDistance = p.shadowDistance;
            shadowSettings.cascadeCount   = p.cascadeCount;
            shadowSettings.split1         = p.split1;
            shadowSettings.split2         = p.split2;
            shadowSettings.split3         = p.split3;
            // ToonShadowControlFeature.beginCameraRendering에서
            // 다음 프레임 shadow map 렌더 전에 자동 반영
        }

        static CinemachineBlendDefinition ToBlendDefinition(CameraParamProfile p)
        {
            var style = p.blendStyle switch
            {
                CameraParamProfile.BlendStyle.Cut      => CinemachineBlendDefinition.Styles.Cut,
                CameraParamProfile.BlendStyle.EaseIn   => CinemachineBlendDefinition.Styles.EaseIn,
                CameraParamProfile.BlendStyle.EaseOut  => CinemachineBlendDefinition.Styles.EaseOut,
                CameraParamProfile.BlendStyle.EaseInOut=> CinemachineBlendDefinition.Styles.EaseInOut,
                CameraParamProfile.BlendStyle.Linear   => CinemachineBlendDefinition.Styles.Linear,
                _                                      => CinemachineBlendDefinition.Styles.EaseInOut,
            };
            return new CinemachineBlendDefinition(style, p.blendDuration);
        }
    }
}
