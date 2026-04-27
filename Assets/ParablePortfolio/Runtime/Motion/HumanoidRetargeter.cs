using UnityEngine;

namespace Parable.Motion
{
    /// <summary>
    /// HumanPoseHandler를 사용해 소스 → 타겟 Humanoid 리타겟팅.
    ///
    /// 동작:
    ///   1. MotionData(muscles[]) → HumanPose 구성
    ///   2. HumanPoseHandler.SetHumanPose() 로 타겟 Animator에 적용
    ///   3. 아바타별 스켈레톤 차이(팔 길이, 허리 높이 등)는 Unity가 자동 보정
    ///
    /// bodyPosition 문제와 수정 방안:
    ///   - Animator가 빈 Controller(muscle=0)를 평가하면 bodyPosition이 음수(-0.09)가 됨
    ///   - GetHumanPose()를 매 프레임 호출하면 이 잘못된 값이 덮어써져 아바타가 지면 아래로 꺼짐
    ///   - 수정: Awake() 시점(Animator 첫 평가 이전)에 올바른 bodyPosition을 캡처해두고
    ///           applyRootMotion=false일 때 항상 이 캡처값을 복원
    ///
    /// Parable 포인트:
    ///   muscles[] 는 -1~1 정규화된 근육값 → 스켈레톤 구조 무관하게 동일하게 적용됨.
    ///   소스(모캡)와 타겟(VRM)의 본 구조가 달라도 Humanoid 중간 포맷으로 통일.
    /// </summary>
    [System.Serializable]
    public class HumanoidRetargeter
    {
        [Tooltip("false면 이 모듈을 건너뜀 (파이프라인 단계별 테스트용)")]
        public bool enabled = true;

        [Range(0f, 1f)]
        [Tooltip("리타겟 적용 강도. 0=원본 포즈 유지, 1=완전 리타겟")]
        public float blendWeight = 1f;

        [Tooltip("true면 MotionData.rootPosition/rootRotation 도 적용.\n" +
                 "테스트 모드에서는 false 권장.")]
        public bool applyRootMotion = false;

        HumanPoseHandler _poseHandler;
        HumanPose        _humanPose;
        Animator         _targetAnimator;

        // Awake()에서 캡처한 올바른 서있는 포즈의 body 위치/회전
        // → Animator가 빈 Controller 평가 후 bodyPosition을 덮어써도 이 값으로 복원
        Vector3    _capturedBodyPosition;
        Quaternion _capturedBodyRotation;
        bool       _bodyPositionCaptured;

        /// <summary>
        /// 타겟 Animator 초기화. Awake()에서 호출해야 함.
        /// Awake() 시점은 Animator 첫 평가 이전이므로 올바른 rest pose의 bodyPosition을 캡처 가능.
        /// </summary>
        public void Initialize(Animator targetAnimator)
        {
            _targetAnimator = targetAnimator;
            if (targetAnimator == null || !targetAnimator.isHuman)
            {
                Debug.LogWarning("[HumanoidRetargeter] 대상이 Humanoid가 아닙니다.");
                return;
            }

            _poseHandler = new HumanPoseHandler(
                targetAnimator.avatar,
                targetAnimator.transform);

            // Awake 시점: 아직 Animator가 첫 평가를 하지 않음
            // → 본들이 Edit Mode의 rest pose 위치에 있음 → 올바른 bodyPosition 캡처
            _poseHandler.GetHumanPose(ref _humanPose);
            _capturedBodyPosition = _humanPose.bodyPosition;
            _capturedBodyRotation = _humanPose.bodyRotation;
            _bodyPositionCaptured = true;

            Debug.Log($"[HumanoidRetargeter] bodyPosition 캡처: {_capturedBodyPosition}");
        }

        /// <summary>
        /// MotionData를 타겟 Animator에 적용. LateUpdate()에서 호출.
        /// </summary>
        public void Apply(MotionData data)
        {
            if (!enabled || !data.isValid) return;
            if (_poseHandler == null)
            {
                Debug.LogWarning("[HumanoidRetargeter] Initialize 먼저 호출 필요.");
                return;
            }

            // 현재 포즈 읽기 (muscles 값 갱신 목적)
            _poseHandler.GetHumanPose(ref _humanPose);

            // muscles 적용 (blendWeight로 보간)
            int count = Mathf.Min(data.muscles.Length, _humanPose.muscles.Length);
            for (int i = 0; i < count; i++)
                _humanPose.muscles[i] = Mathf.Lerp(
                    _humanPose.muscles[i], data.muscles[i], blendWeight);

            if (applyRootMotion)
            {
                // 실제 모캡 연동 시: 소스 루트 위치/회전 적용
                _humanPose.bodyPosition = data.rootPosition;
                _humanPose.bodyRotation = data.rootRotation;
            }
            else if (_bodyPositionCaptured)
            {
                // 테스트 모드: Animator 평가가 덮어쓴 bodyPosition(-0.09)을
                // Awake에서 캡처한 올바른 값(0.96)으로 복원
                _humanPose.bodyPosition = _capturedBodyPosition;
                _humanPose.bodyRotation = _capturedBodyRotation;
            }

            _poseHandler.SetHumanPose(ref _humanPose);
        }

        public void Dispose()
        {
            _poseHandler?.Dispose();
            _poseHandler = null;
        }
    }
}
