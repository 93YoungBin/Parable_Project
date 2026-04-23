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

            var data = new HumanoidPoseData
            {
                muscles      = (float[])input.muscles.Clone(),
                bodyPosition = input.bodyPosition,
                bodyRotation = input.bodyRotation,
                timestamp    = input.timestamp,
                isValid      = true,
                activeMask   = input.activeMask & applyMask,
            };

            if (skeletonProfile != null)
            {
                skeletonProfile.ApplyScale(data.muscles);
                data.bodyPosition = new Vector3(
                    data.bodyPosition.x * skeletonProfile.rootPositionScale,
                    data.bodyPosition.y * skeletonProfile.rootPositionScale + skeletonProfile.rootYOffset,
                    data.bodyPosition.z * skeletonProfile.rootPositionScale);
            }

            _workPose = data.ToHumanPose();
            _targetHandler.SetHumanPose(ref _workPose);

            return data;
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
