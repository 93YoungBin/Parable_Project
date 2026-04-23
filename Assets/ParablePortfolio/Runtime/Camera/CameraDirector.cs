using System;
using System.Collections.Generic;
using UnityEngine;
using Unity.Cinemachine;

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

        public event Action<int, CameraSlot> OnCameraChanged;

        int _activeIndex = -1;
        public int ActiveIndex => _activeIndex;

        void Start()
        {
            // 첫 번째 카메라만 활성화, 나머지 비활성화
            for (int i = 0; i < cameras.Count; i++)
            {
                var vc = cameras[i].virtualCamera;
                if (vc != null) vc.enabled = (i == 0);
            }
            if (cameras.Count > 0) _activeIndex = 0;
        }

        /// <summary>
        /// 카메라 전환. index 범위 밖이면 무시.
        /// AnimationEvent 에서 직접 호출 가능 (int 파라미터 1개).
        /// </summary>
        public void SwitchTo(int index)
        {
            if (index < 0 || index >= cameras.Count) return;
            if (index == _activeIndex) return;

            var next = cameras[index];
            if (next.virtualCamera == null) return;

            // 블렌드 설정 적용 (Brain DefaultBlend 교체)
            if (brain != null && next.profile != null)
                brain.DefaultBlend = ToBlendDefinition(next.profile);

            // 전환: 이전 OFF, 다음 ON
            if (_activeIndex >= 0 && cameras[_activeIndex].virtualCamera != null)
                cameras[_activeIndex].virtualCamera.enabled = false;

            next.virtualCamera.enabled = true;
            ApplyProfile(next.virtualCamera, next.profile);

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

            // Lens
            var lens = vc.Lens;
            lens.FieldOfView   = p.fieldOfView;
            lens.NearClipPlane = p.nearClipPlane;
            lens.FarClipPlane  = p.farClipPlane;
            vc.Lens = lens;

            // Follow spring damping
            var follow = vc.GetComponent<CinemachineFollow>();
            if (follow != null)
            {
                var ts = follow.TrackerSettings;
                ts.PositionDamping = new Vector3(p.followDampingX, p.followDampingY, p.followDampingZ);
                follow.TrackerSettings = ts;
            }

            // Aim damping
            var aim = vc.GetComponent<CinemachineRotationComposer>();
            if (aim != null)
            {
                aim.Damping.x = p.aimDampingH;
                aim.Damping.y = p.aimDampingV;
            }

            // Noise
            var noise = vc.GetComponent<CinemachineBasicMultiChannelPerlin>();
            if (noise != null)
            {
                noise.AmplitudeGain  = p.noiseAmplitude;
                noise.FrequencyGain = p.noiseFrequency;
            }
        }

        static CinemachineBlendDefinition ToBlendDefinition(CameraParamProfile p)
        {
            var style = p.blendStyle switch
            {
                CameraParamProfile.BlendStyle.Cut        => CinemachineBlendDefinition.Styles.Cut,
                CameraParamProfile.BlendStyle.EaseIn     => CinemachineBlendDefinition.Styles.EaseIn,
                CameraParamProfile.BlendStyle.EaseOut    => CinemachineBlendDefinition.Styles.EaseOut,
                CameraParamProfile.BlendStyle.Linear     => CinemachineBlendDefinition.Styles.Linear,
                _                                        => CinemachineBlendDefinition.Styles.EaseInOut,
            };
            return new CinemachineBlendDefinition(style, p.blendDuration);
        }
    }
}
