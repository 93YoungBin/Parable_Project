using UnityEngine;

namespace Parable.Motion
{
    /// <summary>
    /// Foot Grounding — Iterative 방식 (매 프레임 delta 적용).
    ///
    /// 원리:
    ///   1. 첫 프레임 발 Y를 기준으로 캐시
    ///   2. 매 프레임 SetHumanPose 직후 발 Y 측정
    ///   3. delta = 현재 발 Y - 기준 → transform.y -= delta
    ///
    /// 양수 delta = 발 떴음 → transform 아래로 (인사 시)
    /// 음수 delta = 발 박힘 → transform 위로 (idle 복귀 시)
    /// 한 프레임에 수렴, 누적 없음.
    /// </summary>
    [System.Serializable]
    public class FootIKSolver
    {
        [Tooltip("false면 Grounding 비활성")]
        public bool enabled = true;

        [Tooltip("Inspector 호환 더미 파라미터")]
        public float raycastOriginOffset = 0.3f;
        public float raycastDistance     = 1.2f;

        float _refFootY = float.NaN;

        public void ApplyGrounding(Animator animator)
        {
            if (!enabled || animator == null) return;

            float lY = GetFootY(animator, HumanBodyBones.LeftFoot);
            float rY = GetFootY(animator, HumanBodyBones.RightFoot);
            float lowest = Mathf.Min(lY, rY);

            if (float.IsNaN(_refFootY))
            {
                _refFootY = lowest;
                Debug.Log($"[FootIK] 기준 발 높이: {_refFootY:F4}m");
                return;
            }

            float delta = lowest - _refFootY;
            if (Mathf.Abs(delta) < 0.0005f) return;

            // 양/음수 모두 적용 — 인사 시 내려가고 복귀 시 올라옴
            animator.transform.position += Vector3.down * delta;
        }

        public void ResetReference() => _refFootY = float.NaN;

        float GetFootY(Animator anim, HumanBodyBones bone)
        {
            var t = anim.GetBoneTransform(bone);
            return t != null ? t.position.y : 0f;
        }
    }
}
