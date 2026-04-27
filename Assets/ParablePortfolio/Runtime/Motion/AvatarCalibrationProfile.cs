using UnityEngine;

namespace Parable.Motion
{
    /// <summary>
    /// Stage 3 — 아바타별 비율/골격 보정 프로필.
    ///
    /// Parable 아티클 포인트:
    ///   Humanoid 리타겟 후에도 아바타마다 어깨 너비·팔 길이·척추 비율이 다름.
    ///   동일한 퍼포먼스를 여러 아바타에 적용할 때 이 보정 없이는 손 위치가 틀어지거나
    ///   척추 과장이 생김. 아바타별 프로필로 교체하는 것만으로 보정 완료.
    /// </summary>
    [CreateAssetMenu(menuName = "Parable/Avatar Calibration Profile",
                     fileName = "AvatarCalibration_New")]
    public class AvatarCalibrationProfile : ScriptableObject
    {
        [Header("근육 그룹별 스케일 (1.0 = 원본 그대로)")]

        [Tooltip("팔 근육 스케일. 어깨 너비가 좁은 VRM 아바타는 0.8~0.9 권장")]
        [Range(0.5f, 1.5f)] public float armMuscleScale   = 1f;

        [Tooltip("척추/허리 근육 스케일. 과장된 동작을 줄이거나 키울 때 사용")]
        [Range(0.5f, 1.5f)] public float spineMuscleScale = 1f;

        [Tooltip("다리 근육 스케일. 걸음 폭 차이 보정")]
        [Range(0.5f, 1.5f)] public float legMuscleScale   = 1f;

        [Header("추가 오프셋 (보정값 직접 더하기)")]
        [Tooltip("왼쪽 어깨 Down-Up 오프셋. 어깨가 처진 아바타 보정")]
        [Range(-0.5f, 0.5f)] public float leftShoulderOffset  = 0f;
        [Range(-0.5f, 0.5f)] public float rightShoulderOffset = 0f;
    }
}
