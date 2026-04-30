using UnityEngine;

namespace Parable.Motion.Core
{
    /// <summary>
    /// 2단계: 관절 각도(degrees) → Unity Humanoid muscle 값(-1~1) 변환.
    ///
    /// 핵심 계산:
    ///   각 muscle에는 HumanTrait이 정의한 해부학적 가동 범위(min/max°)가 있음.
    ///   raw 각도를 이 범위로 정규화하면 Unity가 사용하는 muscle 공간으로 변환됨.
    ///
    ///   normalized = (angle - min) / (max - min) * 2 - 1
    ///
    ///   예) Left Arm Down-Up (index 39), range: -90° ~ 90°
    ///     angle  80° → ( 80 - (-90) ) / 180 * 2 - 1 = +0.89
    ///     angle -65° → ( -65 - (-90) ) / 180 * 2 - 1 = -0.72
    ///
    /// 이 변환이 포트폴리오 파이프라인의 '리타겟팅' 계산 핵심.
    /// HumanPoseHandler.SetHumanPose()가 이 muscle 값을
    /// 각 아바타의 실제 bone rotation으로 다시 변환.
    /// </summary>
    public class NormalizationStage : HumanoidPipelineStage
    {
        // 빌드 시 한 번만 계산 (HumanTrait은 변하지 않음)
        float[] _muscleMin;
        float[] _muscleMax;

        void Awake()
        {
            int count = HumanTrait.MuscleCount;
            _muscleMin = new float[count];
            _muscleMax = new float[count];

            for (int i = 0; i < count; i++)
            {
                _muscleMin[i] = HumanTrait.GetMuscleDefaultMin(i);
                _muscleMax[i] = HumanTrait.GetMuscleDefaultMax(i);
            }
        }

        protected override HumanoidPoseData Process(HumanoidPoseData input)
        {
            if (!input.isValid) return null;

            var output = new HumanoidPoseData(input);

            for (int i = 0; i < input.muscles.Length; i++)
            {
                float min   = _muscleMin[i];
                float max   = _muscleMax[i];
                float range = max - min;

                // 가동 범위가 없는 관절 (고정 조인트 등) — 0으로 처리
                if (range < 0.001f)
                {
                    output.muscles[i] = 0f;
                    continue;
                }

                // 핵심 정규화 공식
                float normalized = (input.muscles[i] - min) / range * 2f - 1f;

                output.muscles[i] = Mathf.Clamp(normalized, -1f, 1f);
            }

            return output;
        }

#if UNITY_EDITOR
        [ContextMenu("Log Muscle Ranges (Debug)")]
        void LogMuscleRanges()
        {
            if (_muscleMin == null) { Debug.Log("플레이 중에 실행하세요"); return; }

            var sb = new System.Text.StringBuilder();
            sb.AppendLine("[NormalizationStage] Muscle 가동 범위 (HumanTrait 기본값)");
            for (int i = 0; i < _muscleMin.Length; i++)
            {
                sb.AppendLine(
                    $"  [{i:D2}] {HumanTrait.MuscleName[i],-35} " +
                    $"{_muscleMin[i]:F1}° ~ {_muscleMax[i]:F1}°");
            }
            Debug.Log(sb.ToString());
        }
#endif
    }
}
