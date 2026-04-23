using System;
using UnityEngine;

namespace Parable.Motion
{
    /// <summary>
    /// 아바타별 스켈레톤 비율 보정 프로파일.
    ///
    /// 문제:
    ///   소스(퍼포머)와 대상(VRM 아바타)의 팔/다리 길이 비율이 다를 때
    ///   muscle 값을 그대로 적용하면 손이 몸을 뚫거나 발이 뜨는 현상 발생.
    ///
    /// 해결:
    ///   신체 부위별 muscle 값에 scale 계수 적용.
    ///   예) 아바타 팔이 퍼포머보다 10% 짧으면 ArmMuscleScale = 0.9
    ///
    /// 사용:
    ///   아바타별로 에셋 하나씩 생성 → RetargetingStage에 연결.
    /// </summary>
    [CreateAssetMenu(fileName = "SkeletonOffsetProfile",
                     menuName  = "Parable/Skeleton Offset Profile")]
    public class SkeletonOffsetProfile : ScriptableObject
    {
        [Header("Arm")]
        [Range(0.5f, 1.5f)]
        [Tooltip("아바타 팔 길이 / 소스 팔 길이. 짧으면 1 미만")]
        public float leftArmMuscleScale  = 1f;
        [Range(0.5f, 1.5f)]
        public float rightArmMuscleScale = 1f;

        [Header("Leg")]
        [Range(0.5f, 1.5f)]
        public float leftLegMuscleScale  = 1f;
        [Range(0.5f, 1.5f)]
        public float rightLegMuscleScale = 1f;

        [Header("Spine")]
        [Range(0.5f, 1.5f)]
        [Tooltip("상체 비율 (키 차이 보정)")]
        public float spineMuscleScale = 1f;

        [Header("Finger")]
        [Range(0.5f, 1.5f)]
        public float fingerMuscleScale = 1f;

        [Header("Root")]
        [Tooltip("루트 Y 오프셋 (바닥 위치 보정, 미터 단위)")]
        public float rootYOffset = 0f;
        [Range(0.5f, 2f)]
        [Tooltip("루트 이동 스케일 (신장 차이 보정)")]
        public float rootPositionScale = 1f;

        // HumanTrait muscle 인덱스 기준 범위 (Unity 기준, 버전에 따라 상이할 수 있음)
        // 런타임에 HumanTrait.MuscleName으로 동적 매핑
        [NonSerialized] public int[] LeftArmRange;
        [NonSerialized] public int[] RightArmRange;
        [NonSerialized] public int[] LeftLegRange;
        [NonSerialized] public int[] RightLegRange;
        [NonSerialized] public int[] SpineRange;
        [NonSerialized] public int[] FingerRange;

        public void BuildRanges()
        {
            LeftArmRange  = FindMuscleIndices("Left",  "Arm",    "Hand", "Thumb", "Index", "Middle", "Ring", "Little");
            RightArmRange = FindMuscleIndices("Right", "Arm",    "Hand", "Thumb", "Index", "Middle", "Ring", "Little");
            LeftLegRange  = FindMuscleIndices("Left",  "Leg",    "Foot", "Toes");
            RightLegRange = FindMuscleIndices("Right", "Leg",    "Foot", "Toes");
            SpineRange    = FindMuscleIndices("",       "Spine",  "Chest", "Neck", "Head");
            FingerRange   = FindMuscleIndices("",       "Thumb",  "Index", "Middle", "Ring", "Little");
        }

        static int[] FindMuscleIndices(string sideKeyword, params string[] keywords)
        {
            var result = new System.Collections.Generic.List<int>();
            for (int i = 0; i < HumanTrait.MuscleCount; i++)
            {
                string name = HumanTrait.MuscleName[i];
                bool sideMatch = string.IsNullOrEmpty(sideKeyword) || name.Contains(sideKeyword);
                bool keyMatch  = false;
                foreach (var kw in keywords)
                    if (name.Contains(kw)) { keyMatch = true; break; }
                if (sideMatch && keyMatch)
                    result.Add(i);
            }
            return result.ToArray();
        }

        public void ApplyScale(float[] muscles)
        {
            ScaleRange(muscles, LeftArmRange,  leftArmMuscleScale);
            ScaleRange(muscles, RightArmRange, rightArmMuscleScale);
            ScaleRange(muscles, LeftLegRange,  leftLegMuscleScale);
            ScaleRange(muscles, RightLegRange, rightLegMuscleScale);
            ScaleRange(muscles, SpineRange,    spineMuscleScale);
        }

        static void ScaleRange(float[] muscles, int[] indices, float scale)
        {
            if (indices == null) return;
            foreach (int i in indices)
                if (i < muscles.Length)
                    muscles[i] *= scale;
        }
    }
}