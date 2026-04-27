using UnityEngine;

namespace Parable.Motion
{
    /// <summary>
    /// Stage 3 — 아바타별 골격/비율 보정.
    ///
    /// Parable 아티클:
    ///   "Avatar-Specific Retargeting — 개별 아바타의 비율/골격에 맞춘 추가 최적화 레이어"
    ///   Stage 2 (Humanoid Retargeting)까지 끝나도 팔 길이·어깨 너비가 달라
    ///   손 위치가 틀어지는 문제를 이 단계에서 per-avatar 프로필로 보정.
    ///
    /// 독립 테스트:
    ///   enabled = false → Stage 2까지만 적용된 포즈 확인
    ///   enabled = true  → 아바타별 보정이 가해진 포즈 확인
    ///   calibration 프로필 교체만으로 아바타 변경 대응
    /// </summary>
    [System.Serializable]
    public class AvatarSpecificStage
    {
        [Tooltip("false면 이 단계를 건너뜀 (파이프라인 단계별 독립 테스트)")]
        public bool enabled = false;

        public AvatarCalibrationProfile calibration;

        // HumanTrait 기준 muscle 인덱스 그룹
        // (실측값: HumanTrait.MuscleName[] 직접 확인)
        static readonly int[] SPINE_IDX = { 0, 1, 2, 3, 4, 5, 6, 7, 8 };
        static readonly int[] LEGS_IDX  = { 21, 22, 23, 24, 25, 26, 27, 28,
                                             29, 30, 31, 32, 33, 34, 35, 36 };
        static readonly int[] ARMS_IDX  = { 37, 38, 39, 40, 41, 42, 43, 44, 45,
                                             46, 47, 48, 49, 50, 51, 52, 53, 54 };

        // 어깨 인덱스 (Down-Up)
        const int LEFT_SHOULDER_IDX  = 37;
        const int RIGHT_SHOULDER_IDX = 46;

        /// <summary>
        /// HumanPose를 직접 수정 (in-place). LateUpdate에서 호출.
        /// </summary>
        public void Process(ref HumanPose pose)
        {
            if (calibration == null) return;

            ApplyScale(ref pose, SPINE_IDX, calibration.spineMuscleScale);
            ApplyScale(ref pose, ARMS_IDX,  calibration.armMuscleScale);
            ApplyScale(ref pose, LEGS_IDX,  calibration.legMuscleScale);

            // 어깨 오프셋 추가 보정
            ApplyOffset(ref pose, LEFT_SHOULDER_IDX,  calibration.leftShoulderOffset);
            ApplyOffset(ref pose, RIGHT_SHOULDER_IDX, calibration.rightShoulderOffset);
        }

        static void ApplyScale(ref HumanPose pose, int[] indices, float scale)
        {
            if (Mathf.Approximately(scale, 1f)) return;
            foreach (int i in indices)
                if (i < pose.muscles.Length)
                    pose.muscles[i] = Mathf.Clamp(pose.muscles[i] * scale, -1f, 1f);
        }

        static void ApplyOffset(ref HumanPose pose, int idx, float offset)
        {
            if (idx >= pose.muscles.Length || Mathf.Approximately(offset, 0f)) return;
            pose.muscles[idx] = Mathf.Clamp(pose.muscles[idx] + offset, -1f, 1f);
        }
    }
}
