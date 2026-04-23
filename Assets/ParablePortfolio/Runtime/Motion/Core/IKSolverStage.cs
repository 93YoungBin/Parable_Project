using UnityEngine;

namespace Parable.Motion.Core
{
    public class IKSolverStage : HumanoidPipelineStage
    {
        [Header("Avatar")]
        public Animator avatarAnimator;

        [Header("IK Effectors")]
        public Transform leftHandTarget;
        public Transform rightHandTarget;
        public Transform leftFootTarget;
        public Transform rightFootTarget;
        public Transform headLookTarget;

        [Header("IK Weights")]
        [Range(0f, 1f)] public float leftHandWeight  = 0f;
        [Range(0f, 1f)] public float rightHandWeight = 0f;
        [Range(0f, 1f)] public float leftFootWeight  = 0f;
        [Range(0f, 1f)] public float rightFootWeight = 0f;
        [Range(0f, 1f)] public float headLookWeight  = 0f;

        [Range(0f, 0.01f)] public float ikEpsilon = 0.0001f;

        bool _hasPending;

        Transform _lUpperArm, _lForearm, _lHand;
        Transform _rUpperArm, _rForearm, _rHand;
        Transform _lThigh,    _lShin,    _lFoot;
        Transform _rThigh,    _rShin,    _rFoot;
        Transform _head;

        void Awake()
        {
            if (avatarAnimator == null)
                avatarAnimator = GetComponent<Animator>();

            if (avatarAnimator != null)
            {
                _lUpperArm = avatarAnimator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
                _lForearm  = avatarAnimator.GetBoneTransform(HumanBodyBones.LeftLowerArm);
                _lHand     = avatarAnimator.GetBoneTransform(HumanBodyBones.LeftHand);
                _rUpperArm = avatarAnimator.GetBoneTransform(HumanBodyBones.RightUpperArm);
                _rForearm  = avatarAnimator.GetBoneTransform(HumanBodyBones.RightLowerArm);
                _rHand     = avatarAnimator.GetBoneTransform(HumanBodyBones.RightHand);
                _lThigh    = avatarAnimator.GetBoneTransform(HumanBodyBones.LeftUpperLeg);
                _lShin     = avatarAnimator.GetBoneTransform(HumanBodyBones.LeftLowerLeg);
                _lFoot     = avatarAnimator.GetBoneTransform(HumanBodyBones.LeftFoot);
                _rThigh    = avatarAnimator.GetBoneTransform(HumanBodyBones.RightUpperLeg);
                _rShin     = avatarAnimator.GetBoneTransform(HumanBodyBones.RightLowerLeg);
                _rFoot     = avatarAnimator.GetBoneTransform(HumanBodyBones.RightFoot);
                _head      = avatarAnimator.GetBoneTransform(HumanBodyBones.Head);
            }
        }

        protected override HumanoidPoseData Process(HumanoidPoseData input)
        {
            _hasPending = input.isValid;
            return input;
        }

        void LateUpdate()
        {
            if (!_hasPending) return;
            _hasPending = false;

            if (leftHandWeight  > 0f && leftHandTarget  != null)
                SolveTwoBoneIK(_lUpperArm, _lForearm, _lHand, leftHandTarget.position, leftHandWeight);

            if (rightHandWeight > 0f && rightHandTarget != null)
                SolveTwoBoneIK(_rUpperArm, _rForearm, _rHand, rightHandTarget.position, rightHandWeight);

            if (leftFootWeight  > 0f && leftFootTarget  != null)
                SolveTwoBoneIK(_lThigh, _lShin, _lFoot, leftFootTarget.position, leftFootWeight);

            if (rightFootWeight > 0f && rightFootTarget != null)
                SolveTwoBoneIK(_rThigh, _rShin, _rFoot, rightFootTarget.position, rightFootWeight);

            if (headLookWeight  > 0f && headLookTarget  != null && _head != null)
                SolveLookAt(_head, headLookTarget.position, headLookWeight);
        }

        void SolveTwoBoneIK(Transform upper, Transform mid, Transform tip,
                            Vector3 targetPos, float weight)
        {
            if (upper == null || mid == null || tip == null) return;

            float upperLen = Vector3.Distance(upper.position, mid.position);
            float lowerLen = Vector3.Distance(mid.position, tip.position);
            float maxReach = upperLen + lowerLen;
            Vector3 root   = upper.position;
            Vector3 target = Vector3.Lerp(tip.position, targetPos, weight);
            float   dist   = (target - root).magnitude;

            if (dist > maxReach - ikEpsilon)
                target = root + (target - root).normalized * (maxReach - ikEpsilon);

            dist = Mathf.Clamp(Vector3.Distance(root, target), ikEpsilon, maxReach - ikEpsilon);

            float cosAngle = (upperLen * upperLen + dist * dist - lowerLen * lowerLen)
                             / (2f * upperLen * dist);
            float angle    = Mathf.Acos(Mathf.Clamp(cosAngle, -1f, 1f)) * Mathf.Rad2Deg;

            Vector3 limb       = (target - root).normalized;
            Vector3 bendUp     = Mathf.Abs(Vector3.Dot(limb, upper.up)) < 0.9f ? upper.up : upper.forward;
            Vector3 bendNormal = Vector3.Cross(limb, bendUp).normalized;

            upper.rotation = Quaternion.FromToRotation(upper.TransformDirection(Vector3.forward), limb) * upper.rotation;
            upper.rotation = Quaternion.AngleAxis(-angle, upper.TransformDirection(bendNormal)) * upper.rotation;

            Vector3 midToTarget = (target - mid.position).normalized;
            Vector3 midToTip    = (tip.position - mid.position).normalized;
            if (midToTip.sqrMagnitude > 0f)
                mid.rotation = Quaternion.FromToRotation(midToTip, midToTarget) * mid.rotation;
        }

        void SolveLookAt(Transform bone, Vector3 targetPos, float weight)
        {
            Vector3 dir = (targetPos - bone.position).normalized;
            if (dir.sqrMagnitude < ikEpsilon) return;
            bone.rotation = Quaternion.Slerp(bone.rotation, Quaternion.LookRotation(dir, Vector3.up), weight);
        }

        public void SetAvatarAnimator(Animator animator)
        {
            avatarAnimator = animator;
            if (avatarAnimator == null) return;
            _lUpperArm = avatarAnimator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
            _lForearm  = avatarAnimator.GetBoneTransform(HumanBodyBones.LeftLowerArm);
            _lHand     = avatarAnimator.GetBoneTransform(HumanBodyBones.LeftHand);
            _rUpperArm = avatarAnimator.GetBoneTransform(HumanBodyBones.RightUpperArm);
            _rForearm  = avatarAnimator.GetBoneTransform(HumanBodyBones.RightLowerArm);
            _rHand     = avatarAnimator.GetBoneTransform(HumanBodyBones.RightHand);
            _lThigh    = avatarAnimator.GetBoneTransform(HumanBodyBones.LeftUpperLeg);
            _lShin     = avatarAnimator.GetBoneTransform(HumanBodyBones.LeftLowerLeg);
            _lFoot     = avatarAnimator.GetBoneTransform(HumanBodyBones.LeftFoot);
            _rThigh    = avatarAnimator.GetBoneTransform(HumanBodyBones.RightUpperLeg);
            _rShin     = avatarAnimator.GetBoneTransform(HumanBodyBones.RightLowerLeg);
            _rFoot     = avatarAnimator.GetBoneTransform(HumanBodyBones.RightFoot);
            _head      = avatarAnimator.GetBoneTransform(HumanBodyBones.Head);
        }
    }
}
