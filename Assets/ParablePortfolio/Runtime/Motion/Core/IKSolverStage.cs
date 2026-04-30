using UnityEngine;

namespace Parable.Motion.Core
{
    /// <summary>
    /// 4단계: IK 보정 — LateUpdate에서 뼈를 직접 조작해 즉시 확정.
    ///
    /// 실행 흐름 (패러블 아티클 구조):
    ///   [Update]     RawPmocapSource → Receive() 동기 체인 → Process() → _hasPending = true
    ///   [LateUpdate] IKSolverStage.LateUpdate() → 레이캐스트 → 2-bone IK → 뼈 즉시 확정
    ///
    /// 발 그라운딩:
    ///   레이캐스트로 지면을 탐지하고 2-bone IK(허벅지→정강이→발)로 발을 붙임.
    ///   footHeightOffset: 아바타마다 발목 본과 발바닥 사이 간격이 다름 → Inspector에서 조정.
    /// </summary>
    public class IKSolverStage : HumanoidPipelineStage
    {
        [Header("Avatar")]
        public Animator avatarAnimator;

        [Header("Foot Grounding")]
        public bool groundingEnabled = true;
        [Range(0f, 1f)] public float footWeight = 1f;
        [Tooltip("발목 본 위치와 실제 발바닥 사이 오프셋 (m). 아바타마다 조정.")]
        public float footHeightOffset = 0.08f;
        [Tooltip("충돌 감지할 레이어")]
        public LayerMask groundLayerMask = ~0;
        [Tooltip("경사면에서 발 회전 정렬 강도 (0 = 무시)")]
        [Range(0f, 1f)] public float footRotationBlend = 0f;

        [Header("Debug")]
        public bool showGizmos = true;

        // 레이캐스트 내부 상수 — Inspector 노출 불필요
        const float RAYCAST_ORIGIN = 0.5f;   // 발에서 레이캐스트 시작 높이
        const float RAYCAST_DIST   = 1.5f;   // 최대 탐지 거리
        const float IK_EPSILON     = 0.0001f;

        bool _hasPending;

        Transform _lThigh, _lShin, _lFoot;
        Transform _rThigh, _rShin, _rFoot;

        // 디버그용 — 마지막 레이캐스트 결과 캐시
        Vector3 _lastLFootTarget, _lastRFootTarget;
        bool    _lastLFootHit,    _lastRFootHit;

        void Awake()
        {
            if (avatarAnimator == null)
                avatarAnimator = GetComponent<Animator>();

            if (avatarAnimator != null)
            {
                _lThigh = avatarAnimator.GetBoneTransform(HumanBodyBones.LeftUpperLeg);
                _lShin  = avatarAnimator.GetBoneTransform(HumanBodyBones.LeftLowerLeg);
                _lFoot  = avatarAnimator.GetBoneTransform(HumanBodyBones.LeftFoot);
                _rThigh = avatarAnimator.GetBoneTransform(HumanBodyBones.RightUpperLeg);
                _rShin  = avatarAnimator.GetBoneTransform(HumanBodyBones.RightLowerLeg);
                _rFoot  = avatarAnimator.GetBoneTransform(HumanBodyBones.RightFoot);
            }
        }

        protected override HumanoidPoseData Process(HumanoidPoseData input)
        {
            // 실제 IK는 LateUpdate에서 실행 — 여기선 플래그만 세팅
            _hasPending = input.isValid;
            return input;
        }

        void LateUpdate()
        {
            if (!_hasPending || !groundingEnabled || footWeight <= 0f) return;
            _hasPending = false;

            ApplyFootGrounding(_lThigh, _lShin, _lFoot, ref _lastLFootTarget, ref _lastLFootHit);
            ApplyFootGrounding(_rThigh, _rShin, _rFoot, ref _lastRFootTarget, ref _lastRFootHit);
        }

        /// <summary>
        /// 레이캐스트로 지면을 탐지하고 2-bone IK로 발을 붙임.
        ///   ① 발 위 RAYCAST_ORIGIN 높이에서 아래로 레이캐스트
        ///   ② hit.point + footHeightOffset → IK 목표 위치
        ///   ③ SolveTwoBoneIK(허벅지→정강이→발)
        ///   ④ footRotationBlend > 0 이면 지면 법선에 발 회전 정렬
        /// </summary>
        void ApplyFootGrounding(Transform thigh, Transform shin, Transform foot,
                                ref Vector3 lastTarget, ref bool lastHit)
        {
            if (thigh == null || shin == null || foot == null) return;

            Vector3 origin = foot.position + Vector3.up * RAYCAST_ORIGIN;
            lastHit = Physics.Raycast(origin, Vector3.down,
                                      out RaycastHit hit,
                                      RAYCAST_ORIGIN + RAYCAST_DIST,
                                      groundLayerMask);
            if (!lastHit) return;

            Vector3 targetPos = hit.point + Vector3.up * footHeightOffset;
            lastTarget = targetPos;

            // 무릎이 앞으로 굽히도록 아바타 forward를 pole vector로 전달
            Vector3 poleDir = avatarAnimator != null
                ? avatarAnimator.transform.forward : Vector3.forward;

            SolveTwoBoneIK(thigh, shin, foot, targetPos, poleDir);

            if (footRotationBlend > 0f)
            {
                Quaternion slopeRot = Quaternion.FromToRotation(foot.up, hit.normal) * foot.rotation;
                foot.rotation = Quaternion.Slerp(foot.rotation, slopeRot,
                                                 footWeight * footRotationBlend);
            }
        }

        /// <summary>
        /// 2-bone IK 솔버 (코사인 법칙, 닫힌 해).
        ///
        /// 뼈 로컬 축을 가정하지 않고 월드 포지션에서 방향을 직접 계산.
        /// poleDir로 중간 관절(무릎/팔꿈치)이 굽힐 방향을 명시적으로 제어.
        ///
        /// poleDir 없으면 수학적으로 해가 두 개(앞/뒤) → 무릎이 뒤로 꺾일 수 있음.
        /// </summary>
        void SolveTwoBoneIK(Transform upper, Transform mid, Transform tip,
                            Vector3 targetPos, Vector3 poleDir)
        {
            if (upper == null || mid == null || tip == null) return;

            float upperLen = Vector3.Distance(upper.position, mid.position);
            float lowerLen = Vector3.Distance(mid.position, tip.position);
            if (upperLen < IK_EPSILON || lowerLen < IK_EPSILON) return;

            float   maxReach = upperLen + lowerLen;
            Vector3 root     = upper.position;
            Vector3 target   = Vector3.Lerp(tip.position, targetPos, footWeight);

            // 최대 도달 거리 클램프
            float dist = (target - root).magnitude;
            if (dist > maxReach - IK_EPSILON)
                target = root + (target - root).normalized * (maxReach - IK_EPSILON);
            dist = Mathf.Clamp((target - root).magnitude, IK_EPSILON, maxReach - IK_EPSILON);

            // 코사인 법칙 → upper 뼈의 굽힘 각도
            float cosAngle = (upperLen * upperLen + dist * dist - lowerLen * lowerLen)
                             / (2f * upperLen * dist);
            float angle    = Mathf.Acos(Mathf.Clamp(cosAngle, -1f, 1f)) * Mathf.Rad2Deg;

            Vector3 targetDir = (target - root).normalized;

            // pole을 targetDir 수직 평면에 투영 → 굽힘 축(bendNormal) 계산
            Vector3 pole = Vector3.ProjectOnPlane(poleDir, targetDir);
            if (pole.sqrMagnitude < 0.001f)
                pole = Vector3.ProjectOnPlane(Vector3.forward, targetDir);
            pole = pole.normalized;

            Vector3 bendNormal = Vector3.Cross(targetDir, pole).normalized;

            // upper 뼈: 실제 포지션 기반 방향 → 목표 방향으로 회전
            Vector3 currentUpperDir = (mid.position - upper.position).normalized;
            Vector3 desiredUpperDir = Quaternion.AngleAxis(angle, bendNormal) * targetDir;
            upper.rotation = Quaternion.FromToRotation(currentUpperDir, desiredUpperDir)
                             * upper.rotation;

            // mid 뼈: tip이 target을 정확히 향하도록 보정
            Vector3 currentMidDir = (tip.position - mid.position).normalized;
            Vector3 desiredMidDir = (target - mid.position).normalized;
            if (currentMidDir.sqrMagnitude > 0.001f && desiredMidDir.sqrMagnitude > 0.001f)
                mid.rotation = Quaternion.FromToRotation(currentMidDir, desiredMidDir)
                               * mid.rotation;
        }

        public void SetAvatarAnimator(Animator animator)
        {
            avatarAnimator = animator;
            if (avatarAnimator == null) return;
            _lThigh = avatarAnimator.GetBoneTransform(HumanBodyBones.LeftUpperLeg);
            _lShin  = avatarAnimator.GetBoneTransform(HumanBodyBones.LeftLowerLeg);
            _lFoot  = avatarAnimator.GetBoneTransform(HumanBodyBones.LeftFoot);
            _rThigh = avatarAnimator.GetBoneTransform(HumanBodyBones.RightUpperLeg);
            _rShin  = avatarAnimator.GetBoneTransform(HumanBodyBones.RightLowerLeg);
            _rFoot  = avatarAnimator.GetBoneTransform(HumanBodyBones.RightFoot);
        }

#if UNITY_EDITOR
        void OnDrawGizmos()
        {
            if (!Application.isPlaying || !showGizmos) return;

            if (_lFoot != null)
            {
                Gizmos.color = _lastLFootHit ? Color.green : Color.red;
                Gizmos.DrawLine(_lFoot.position + Vector3.up * RAYCAST_ORIGIN,
                                _lFoot.position - Vector3.up * RAYCAST_DIST);
                if (_lastLFootHit) Gizmos.DrawWireSphere(_lastLFootTarget, 0.03f);
            }

            if (_rFoot != null)
            {
                Gizmos.color = _lastRFootHit ? Color.cyan : Color.red;
                Gizmos.DrawLine(_rFoot.position + Vector3.up * RAYCAST_ORIGIN,
                                _rFoot.position - Vector3.up * RAYCAST_DIST);
                if (_lastRFootHit) Gizmos.DrawWireSphere(_lastRFootTarget, 0.03f);
            }
        }
#endif
    }
}
