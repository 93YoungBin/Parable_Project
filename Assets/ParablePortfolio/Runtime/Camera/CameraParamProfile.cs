using UnityEngine;

namespace Parable.Camera
{
    /// <summary>
    /// 물리 기반 카메라 파라미터 프로필.
    ///
    /// Parable 아키텍처 포인트:
    ///   - 방송/공연 현장 단위(mm, f-stop)로 파라미터 관리 → 연출자가 실제 카메라 언어로 소통 가능
    ///   - Cinemachine Physical Camera 모드로 주입 → FOV는 focalLength + sensorSize로 역산
    ///   - Cinemachine 의존성 없이 순수 데이터만 보관 → 스테이지/카메라 분리 테스트 가능
    /// </summary>
    [CreateAssetMenu(menuName = "Parable/Camera Param Profile", fileName = "CamProfile_New")]
    public class CameraParamProfile : ScriptableObject
    {
        [Header("Physical Lens")]
        [Tooltip("초점 거리 (mm). 16=광각, 35=준광각, 50=표준, 85=인물, 135=망원\n" +
                 "FOV는 focalLength + sensorSize 조합으로 자동 계산됨")]
        [Range(10f, 300f)] public float focalLength = 50f;

        [Tooltip("이미지 센서 크기 (mm). 기본값은 Super-35 (방송 카메라 표준)\n" +
                 "APS-C: 23.5×15.6 / Full-Frame: 36×24 / Super-35: 24.89×18.66")]
        public Vector2 sensorSize = new Vector2(24.89f, 18.66f);

        [Tooltip("조리개 (f-stop). 낮을수록 피사계심도 얕아짐(배경 흐림 증가)\n" +
                 "f/1.4=매우 얕음, f/2.8=얕음, f/5.6=보통, f/11=깊음")]
        [Range(1f, 22f)] public float aperture = 5.6f;

        [Tooltip("초점 거리 (m). 피사체까지의 실제 거리에 맞게 설정")]
        [Min(0.1f)] public float focusDistance = 5f;

        [Tooltip("ISO 감도. 높을수록 밝지만 노이즈 증가 (DOF 계산에 영향)")]
        [Range(100, 6400)] public int iso = 200;

        [Tooltip("셔터 속도 (초). 1/50 = 0.02, 1/100 = 0.01 (모션 블러 영향)")]
        [Range(0.0005f, 0.5f)] public float shutterSpeed = 0.02f;

        [Header("Clip Planes")]
        [Range(0.01f, 5f)]   public float nearClipPlane = 0.3f;
        [Range(10f, 5000f)]  public float farClipPlane  = 1000f;

        [Header("Follow Spring")]
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

        [Header("Noise")]
        [Range(0f, 5f)]   public float noiseAmplitude = 0f;
        [Range(0.1f, 5f)] public float noiseFrequency = 1f;

        [Header("Blend")]
        [Range(0f, 3f)] public float blendDuration = 0.5f;
        public BlendStyle blendStyle = BlendStyle.EaseInOut;

        public enum BlendStyle { Cut, EaseIn, EaseOut, EaseInOut, Linear }

        [Header("Shadow Override")]
        [Tooltip("ON이면 이 카메라로 전환 시 Shadow 설정을 덮어씀")]
        public bool overrideShadow = false;

        [Tooltip("이 카메라 시점에서의 Shadow 거리 (m). 클로즈업은 짧게, 와이드는 길게.")]
        [Min(0f)] public float shadowDistance = 30f;

        [Tooltip("Cascade 개수")]
        [Range(1, 4)] public int cascadeCount = 4;

        [Tooltip("Cascade 분할 비율 1")]
        [Range(0.01f, 0.98f)] public float split1 = 0.067f;

        [Tooltip("Cascade 분할 비율 2")]
        [Range(0.02f, 0.99f)] public float split2 = 0.200f;

        [Tooltip("Cascade 분할 비율 3")]
        [Range(0.03f, 0.99f)] public float split3 = 0.467f;

        /// <summary>
        /// focalLength + sensorSize → FOV(도) 변환.
        /// Cinemachine FieldOfView 설정 시 사용.
        /// </summary>
        public float ComputedFOV()
        {
            if (focalLength <= 0f) return 60f;
            float halfFOV = Mathf.Atan(sensorSize.y * 0.5f / focalLength) * Mathf.Rad2Deg;
            return halfFOV * 2f;
        }
    }
}
