using UnityEngine;

namespace Parable.Camera
{
    /// <summary>
    /// 물리 기반 카메라 파라미터 프로필.
    ///
    /// Parable 아키텍처 포인트:
    ///   - 카메라 "물리 특성"을 에셋으로 분리 → 런타임에 CameraDirector 가 CinemachineCamera 에 주입
    ///   - 연출자가 Inspector 에서 프리셋 단위로 관리 (Close-up / Medium / Wide / Action 등)
    ///   - Cinemachine 의존성 없이 순수 데이터만 보관 → 스테이지/카메라 분리 테스트 가능
    /// </summary>
    [CreateAssetMenu(menuName = "Parable/Camera Param Profile", fileName = "CamProfile_New")]
    public class CameraParamProfile : ScriptableObject
    {
        [Header("Lens")]
        [Range(10f, 120f)] public float fieldOfView     = 60f;
        [Range(0.01f, 5f)] public float nearClipPlane   = 0.3f;
        [Range(10f, 5000f)] public float farClipPlane   = 1000f;

        [Header("Follow Spring (물리 감쇠)")]
        [Tooltip("XYZ 축별 위치 감쇠. 값이 클수록 즉각 반응, 0에 가까울수록 부드럽게 추적")]
        [Range(0f, 20f)] public float followDampingX = 1f;
        [Range(0f, 20f)] public float followDampingY = 1f;
        [Range(0f, 20f)] public float followDampingZ = 1f;
        [Tooltip("Look-ahead 시간 (초). 피사체 이동 방향을 예측해 카메라를 앞당김")]
        [Range(0f, 1f)] public float lookAheadTime = 0f;
        public bool lookAheadIgnoreY = true;

        [Header("Aim Damping")]
        [Range(0f, 20f)] public float aimDampingH = 0.5f;
        [Range(0f, 20f)] public float aimDampingV = 0.5f;

        [Header("Noise (핸드헬드 시뮬레이션)")]
        [Range(0f, 5f)]  public float noiseAmplitude = 0f;
        [Range(0.1f, 5f)] public float noiseFrequency = 1f;

        [Header("Blend (이 카메라로 전환 시)")]
        [Range(0f, 3f)] public float blendDuration = 0.5f;
        public BlendStyle blendStyle = BlendStyle.EaseInOut;

        public enum BlendStyle { Cut, EaseIn, EaseOut, EaseInOut, Linear }
    }
}
