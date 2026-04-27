using UnityEngine;

namespace Parable.Motion
{
    /// <summary>
    /// Foot IK 솔버. 발이 지면을 뚫거나 허공에 뜨지 않도록 보정.
    ///
    /// 동작:
    ///   1. 발 위치에서 아래로 Raycast
    ///   2. 지면 히트 위치 + 법선 방향으로 IK 목표 설정
    ///   3. Animator.SetIKPosition/Rotation 으로 발 본 보정
    ///
    /// 호출 타이밍: OnAnimatorIK() (LateUpdate 직전, Animator IK 콜백)
    ///   → FK 계산 완료 후 IK 적용 → 같은 프레임 내 본 데이터 확정
    ///
    /// Parable 포인트:
    ///   Update(리타겟) → OnAnimatorIK(FootIK) 순서가 반드시 지켜져야
    ///   리타겟 결과를 IK가 덮어쓰지 않음.
    /// </summary>
    [System.Serializable]
    public class FootIKSolver
    {
        [Tooltip("false면 Foot IK 비활성화 (파이프라인 단계별 테스트용)")]
        public bool enabled = true;

        [Tooltip("발과 지면 사이 최소 오프셋 (m)")]
        [Range(0f, 0.2f)]
        public float footOffset = 0.02f;

        [Tooltip("IK Weight. 0=IK 무효, 1=완전 적용")]
        [Range(0f, 1f)]
        public float ikWeight = 1f;

        [Tooltip("Raycast 시작 높이 오프셋")]
        public float raycastOriginOffset = 0.5f;

        [Tooltip("Raycast 최대 거리")]
        public float raycastDistance = 1.5f;

        [Tooltip("지면 LayerMask")]
        public LayerMask groundLayer = ~0; // 기본: 전체

        /// <summary>
        /// Animator.OnAnimatorIK() 콜백에서 호출.
        /// </summary>
        public void Solve(Animator animator)
        {
            if (!enabled || animator == null) return;

            SolveFoot(animator, AvatarIKGoal.LeftFoot,  HumanBodyBones.LeftFoot);
            SolveFoot(animator, AvatarIKGoal.RightFoot, HumanBodyBones.RightFoot);
        }

        void SolveFoot(Animator animator, AvatarIKGoal goal, HumanBodyBones bone)
        {
            Transform footTransform = animator.GetBoneTransform(bone);
            if (footTransform == null) return;

            Vector3 origin = footTransform.position + Vector3.up * raycastOriginOffset;

            if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit,
                                raycastDistance + raycastOriginOffset, groundLayer))
            {
                // IK 목표 위치: 히트 지점 + 발 오프셋
                Vector3 ikPos = hit.point + Vector3.up * footOffset;

                // IK 목표 회전: 지면 법선에 맞게 발 회전
                Quaternion ikRot = Quaternion.FromToRotation(Vector3.up, hit.normal)
                                 * footTransform.rotation;

                animator.SetIKPositionWeight(goal, ikWeight);
                animator.SetIKRotationWeight(goal, ikWeight);
                animator.SetIKPosition(goal, ikPos);
                animator.SetIKRotation(goal, ikRot);
            }
            else
            {
                // 지면 없으면 IK 해제
                animator.SetIKPositionWeight(goal, 0f);
                animator.SetIKRotationWeight(goal, 0f);
            }
        }
    }
}
