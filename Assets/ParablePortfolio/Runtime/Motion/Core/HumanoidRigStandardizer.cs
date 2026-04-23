using UnityEngine;

namespace Parable.Motion
{
    /// <summary>
    /// Humanoid Rig 표준화 컴포넌트 — T-2.1 핵심.
    ///
    /// 역할:
    ///   소스 Animator(퍼포머/mocap/애니메이션 클립)의 Humanoid 포즈를
    ///   HumanoidPoseData(중간 포맷)로 변환하여 다음 파이프라인 단계로 전달.
    ///
    /// 구조:
    ///   HumanPoseHandler — Unity가 Humanoid Avatar의 muscle 값을
    ///   월드/로컬 좌표 변환 없이 직접 읽고 쓸 수 있게 해주는 핸들러.
    ///   Avatar가 다른 아바타여도 같은 muscle 인덱스 → 자동 리타겟팅 기반.
    ///
    /// Update()에서 샘플링 → 다음 컴포넌트(T-2.2 클린업)로 전달.
    /// </summary>
    [RequireComponent(typeof(Animator))]
    public class HumanoidRigStandardizer : MonoBehaviour
    {
        [Header("Source")]
        [Tooltip("포즈를 읽어올 Animator (Humanoid Avatar 필수)")]
        public Animator sourceAnimator;

        [Header("Output")]
        [Tooltip("다음 파이프라인 단계 — 비어있으면 로컬 저장만")]
        public HumanoidPipelineStage nextStage;

        [Header("Mask")]
        public PoseMaskFlags activeMask = PoseMaskFlags.FullBody;

        // 현재 프레임 포즈 (읽기 전용으로 외부 참조 가능)
        public HumanoidPoseData CurrentPose { get; private set; }

        HumanPoseHandler _handler;
        HumanPose        _rawPose;
        bool             _initialized;

        void OnEnable()  => TryInitialize();
        void OnDisable() => _handler?.Dispose();

        void TryInitialize()
        {
            if (sourceAnimator == null)
                sourceAnimator = GetComponent<Animator>();

            if (sourceAnimator.avatar == null || !sourceAnimator.avatar.isHuman)
            {
                Debug.LogWarning(
                    $"[Standardizer] {name}: Avatar가 없거나 Humanoid가 아님.");
                enabled = false;
                return;
            }

            _handler     = new HumanPoseHandler(
                sourceAnimator.avatar, sourceAnimator.transform);
            CurrentPose  = new HumanoidPoseData();
            _initialized = true;
        }

        void Update()
        {
            if (!_initialized) return;

            // ① HumanPoseHandler로 현재 프레임 muscle 값 샘플링
            _handler.GetHumanPose(ref _rawPose);

            // ② 중간 포맷으로 변환
            CurrentPose = HumanoidPoseData.FromHumanPose(_rawPose);
            CurrentPose.activeMask = activeMask;

            // ③ 다음 파이프라인 단계로 전달 (T-2.2 클린업 등)
            nextStage?.Receive(CurrentPose);
        }

        void Reset()
        {
            sourceAnimator = GetComponent<Animator>();
        }

#if UNITY_EDITOR
        void OnDrawGizmosSelected()
        {
            if (!_initialized || CurrentPose == null) return;
            // 루트 위치 표시
            UnityEditor.Handles.Label(
                transform.position + Vector3.up * 2.2f,
                $"Pose valid={CurrentPose.isValid}");
        }
#endif
    }
}