using UnityEngine;

namespace Parable.Motion.Core
{
    public class RetargetingStage : HumanoidPipelineStage
    {
        [Header("Target")]
        public Animator targetAnimator;

        [Header("Skeleton Offset")]
        public SkeletonOffsetProfile skeletonProfile;

        [Header("Mask")]
        public PoseMaskFlags applyMask = PoseMaskFlags.FullBody;

        HumanPoseHandler _targetHandler;
        HumanPose        _workPose;

        // 첫 프레임 idle 포즈 캐시
        float[]    _idleMuscles;
        Vector3    _idleBodyPos;   // GetHumanPose가 반환한 월드 기준 hip 위치 (그대로 유지)
        Quaternion _idleBodyRot;
        bool       _idleCached;

        void Awake()
        {
            if (targetAnimator == null)
                targetAnimator = GetComponent<Animator>();

            _targetHandler?.Dispose();
            _targetHandler = null;
            if (targetAnimator != null && targetAnimator.isHuman)
                _targetHandler = new HumanPoseHandler(targetAnimator.avatar, targetAnimator.transform);

            skeletonProfile?.BuildRanges();
        }

        void OnDestroy()
        {
            _targetHandler?.Dispose();
        }

        protected override HumanoidPoseData Process(HumanoidPoseData input)
        {
            if (_targetHandler == null || !input.isValid)
                return input;

            // ── 첫 프레임: idle 포즈 캐시 ────────────────────────────
            // SetHumanPose 이전에 읽어야 오염되지 않은 값을 얻을 수 있음
            if (!_idleCached)
            {
                _targetHandler.GetHumanPose(ref _workPose);
                _idleBodyPos = _workPose.bodyPosition;
                _idleBodyRot = _workPose.bodyRotation;
                _idleMuscles = (float[])_workPose.muscles.Clone();
                _idleCached  = true;
            }

            // ── idle을 베이스로, 마스크된 부위만 input으로 덮어씀 ──
            PoseMaskFlags effectiveMask = input.activeMask & applyMask;

            if (_workPose.muscles == null || _workPose.muscles.Length == 0)
                _workPose.muscles = new float[HumanTrait.MuscleCount];

            System.Array.Copy(_idleMuscles, _workPose.muscles, _idleMuscles.Length);

            // 마스크 영역만 input 값으로 교체
            var temp = new HumanoidPoseData();
            System.Array.Copy(_idleMuscles, temp.muscles, _idleMuscles.Length);
            temp.ApplyMask(effectiveMask, input);
            System.Array.Copy(temp.muscles, _workPose.muscles, temp.muscles.Length);

            // bodyPosition: 첫 프레임 GetHumanPose로 읽은 값 그대로 사용 (월드 공간)
            // → SetHumanPose가 이 좌표로 avatar를 배치하므로 씬 배치 위치가 유지됨
            _workPose.bodyPosition = _idleBodyPos;
            _workPose.bodyRotation = _idleBodyRot;

            if (skeletonProfile != null)
            {
                skeletonProfile.ApplyScale(_workPose.muscles);
                _workPose.bodyPosition = new Vector3(
                    _idleBodyPos.x * skeletonProfile.rootPositionScale,
                    _idleBodyPos.y * skeletonProfile.rootPositionScale + skeletonProfile.rootYOffset,
                    _idleBodyPos.z * skeletonProfile.rootPositionScale);
            }

            _targetHandler.SetHumanPose(ref _workPose);

            return input;
        }

        public void SetTargetAvatar(Animator animator)
        {
            targetAnimator = animator;
            _targetHandler?.Dispose();
            _targetHandler = null;
            if (targetAnimator != null && targetAnimator.isHuman)
                _targetHandler = new HumanPoseHandler(targetAnimator.avatar, targetAnimator.transform);
        }
    }
}
