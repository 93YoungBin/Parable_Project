using UnityEngine;

namespace Parable.Motion
{
    /// <summary>
    /// 파이프라인 단계 간 전달되는 포즈 데이터.
    /// Unity HumanPose 를 래핑하여 각 모듈이 독립적으로 읽고 씀.
    /// </summary>
    public class MotionData
    {
        /// <summary>
        /// Unity Humanoid 근육값 배열 (-1 ~ 1).
        /// 모든 관절 상태를 표준 포맷으로 표현.
        /// </summary>
        public float[] muscles;

        /// <summary>루트 월드 위치</summary>
        public Vector3 rootPosition;

        /// <summary>루트 월드 회전</summary>
        public Quaternion rootRotation;

        /// <summary>유효한 데이터가 들어있는지 여부</summary>
        public bool isValid;

        public MotionData()
        {
            // Unity Humanoid muscles 배열은 95개
            muscles      = new float[HumanTrait.MuscleCount];
            rootPosition = Vector3.zero;
            rootRotation = Quaternion.identity;
            isValid      = false;
        }

        /// <summary>다른 MotionData 값을 복사</summary>
        public void CopyFrom(MotionData other)
        {
            System.Array.Copy(other.muscles, muscles, muscles.Length);
            rootPosition = other.rootPosition;
            rootRotation = other.rootRotation;
            isValid      = other.isValid;
        }
    }
}
