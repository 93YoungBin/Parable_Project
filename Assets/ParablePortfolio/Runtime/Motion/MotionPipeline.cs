using UnityEngine;

namespace Parable.Motion
{
    /// <summary>
    /// 패러블 아티클 3단계 모션 파이프라인 오케스트레이터.
    ///
    /// ┌─────────────────────────────────────────────────┐
    /// │  [Update 도메인 — Animator 평가 후 LateUpdate]   │
    /// │                                                  │
    /// │  Stage 2: Humanoid Retargeting                  │
    /// │    └→ Animator가 수행 (HumanPose 포맷 자체가 중간 포맷)│
    /// │                                                  │
    /// │  Stage 1: Real-time Cleanup                     │
    /// │    └→ EMA + Outlier Rejection (마커 가림 보정)   │
    /// │                                                  │
    /// │  Stage 3: Avatar-Specific Retargeting           │
    /// │    └→ 아바타별 비율/골격 보정 (프로필 교체)        │
    /// └─────────────────────────────────────────────────┘
    ///
    /// ┌─────────────────────────────────────────────────┐
    /// │  [LateUpdate 별도 도메인]                        │
    /// │  Foot IK — Update 모션 확정 후 지면 보정         │
    /// └─────────────────────────────────────────────────┘
    ///
    /// 핵심 설계:
    ///   - Animator(Idle 클립)가 정상 포즈를 평가 → bodyPosition 올바름
    ///   - GetHumanPose로 읽고 → 파이프라인 처리 → SetHumanPose로 적용
    ///   - bodyPosition은 Animator에서 읽은 값 그대로 유지 → sinking 없음
    ///
    /// 테스트 모드 (모캡 없이 파이프라인 검증):
    ///   injectNoise ON → 의도적 jitter 주입 (실제 모캡 노이즈 시뮬레이션)
    ///   Stage 1 ON  → Cleanup이 jitter 제거하는 것 확인
    ///   Stage 3 ON  → 아바타별 보정 효과 확인
    /// </summary>
    [RequireComponent(typeof(Animator))]
    public class MotionPipeline : MonoBehaviour
    {
        [Header("Stage 1 — Real-time Cleanup")]
        public MotionCleanupModule cleanup = new MotionCleanupModule();

        [Header("Stage 3 — Avatar-Specific Retargeting")]
        public AvatarSpecificStage avatarSpecific = new AvatarSpecificStage();

        [Header("LateUpdate — Foot IK (파이프라인 외부 도메인)")]
        public FootIKSolver footIK = new FootIKSolver();

        [Header("테스트 — 노이즈 주입 (Stage 1 효과 검증용)")]
        [Tooltip("ON: 의도적으로 jitter 주입 → Stage 1 Cleanup 효과를 눈으로 확인\n" +
                 "실제 모캡 없이 파이프라인 동작 검증")]
        public bool injectNoise = false;

        [Tooltip("Gaussian 노이즈 크기. 0.1=소폭 흔들림, 0.5=심한 jitter")]
        [Range(0f, 1f)] public float noiseAmount = 0.15f;

        [Tooltip("아웃라이어(마커 가림) 시뮬레이션 확률. 0.02=2% 확률로 1프레임 값 튐")]
        [Range(0f, 0.1f)] public float outlierProbability = 0.02f;

        // ── 내부 ──────────────────────────────────────────────────────
        Animator         _animator;
        HumanPoseHandler _handler;
        HumanPose        _pose;

        // ── 생명주기 ──────────────────────────────────────────────────

        void Awake()
        {
            _animator = GetComponent<Animator>();

            if (!_animator.isHuman)
            {
                Debug.LogError("[MotionPipeline] Animator가 Humanoid 타입이 아닙니다.");
                enabled = false;
                return;
            }

            _handler = new HumanPoseHandler(_animator.avatar, _animator.transform);
        }

        /// <summary>
        /// LateUpdate: Animator가 이미 Idle 클립을 평가한 뒤 실행.
        ///
        /// 실행 순서:
        ///   [Animator 내부 평가 — Idle 클립 → 올바른 bodyPosition 포함 포즈]
        ///     ↓
        ///   LateUpdate: GetHumanPose (읽기) → 파이프라인 처리 → SetHumanPose (쓰기)
        ///
        /// bodyPosition을 Animator에서 읽어 그대로 유지하므로 sinking 없음.
        /// </summary>
        void LateUpdate()
        {
            // Stage 2: Animator(Humanoid) 평가 결과 읽기
            // → bodyPosition 포함, 올바른 서있는 포즈
            _handler.GetHumanPose(ref _pose);

            // [테스트] 노이즈 주입 — 실제 모캡 jitter/마커 가림 시뮬레이션
            if (injectNoise)
                InjectTestNoise();

            // Stage 1: Real-time Cleanup
            if (cleanup.enabled)
                cleanup.Process(ref _pose);

            // Stage 3: Avatar-Specific Retargeting
            if (avatarSpecific.enabled)
                avatarSpecific.Process(ref _pose);

            // 처리된 포즈 적용
            // bodyPosition은 Animator 읽은 값 그대로 → sinking 없음
            _handler.SetHumanPose(ref _pose);
        }

        /// <summary>
        /// OnAnimatorIK: LateUpdate 이전에 Animator IK 콜백.
        /// Foot IK는 파이프라인 "단계"가 아닌 별도 타이밍 도메인.
        /// </summary>
        void OnAnimatorIK(int layerIndex)
        {
            footIK.Solve(_animator);
        }

        void OnDestroy()
        {
            _handler?.Dispose();
        }

        // ── 테스트 헬퍼 ──────────────────────────────────────────────

        /// <summary>
        /// 의도적 jitter + 마커 가림 주입.
        /// Stage 1 Cleanup이 켜지면 제거되는 것을 확인할 수 있음.
        /// </summary>
        void InjectTestNoise()
        {
            for (int i = 0; i < _pose.muscles.Length; i++)
            {
                // Gaussian-like noise (Box-Muller 근사)
                float noise = (Random.value + Random.value - 1f) * noiseAmount;
                _pose.muscles[i] = Mathf.Clamp(_pose.muscles[i] + noise, -1f, 1f);

                // Outlier: 일정 확률로 값이 끝으로 튐 (마커 가림 시뮬레이션)
                if (outlierProbability > 0f && Random.value < outlierProbability)
                    _pose.muscles[i] = Random.value > 0.5f ? 1f : -1f;
            }
        }

        // ── Gizmos ───────────────────────────────────────────────────

        void OnDrawGizmos()
        {
            if (_animator == null || !footIK.enabled) return;
            DrawFootRay(HumanBodyBones.LeftFoot,  Color.green);
            DrawFootRay(HumanBodyBones.RightFoot, Color.cyan);
        }

        void DrawFootRay(HumanBodyBones bone, Color color)
        {
            var t = _animator.GetBoneTransform(bone);
            if (t == null) return;
            Vector3 origin = t.position + Vector3.up * footIK.raycastOriginOffset;
            Gizmos.color = color;
            Gizmos.DrawLine(origin, origin + Vector3.down * footIK.raycastDistance);
            Gizmos.DrawWireSphere(t.position, 0.03f);
        }
    }
}
