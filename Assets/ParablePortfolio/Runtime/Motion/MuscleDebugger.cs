using UnityEngine;

namespace Parable.Motion
{
    /// <summary>
    /// Play 모드에서 특정 muscle 값을 직접 조작해 방향 확인.
    ///
    /// 사용법:
    ///   1. Avatar GameObject에 추가
    ///   2. Play 모드 진입
    ///   3. Inspector에서 슬라이더 조작 → 아바타 반응 확인
    ///   4. 확인 후 이 컴포넌트 제거
    /// </summary>
    [RequireComponent(typeof(Animator))]
    public class MuscleDebugger : MonoBehaviour
    {
        [Header("ON/OFF")]
        public bool enabled = true;

        [Header("척추")]
        [Range(-1f, 1f)] public float spineFrontBack  = 0f;  // 0 = Spine Front-Back
        [Range(-1f, 1f)] public float spineSideways   = 0f;  // 1 = Spine Left-Right
        [Range(-1f, 1f)] public float chestFrontBack  = 0f;  // 3 = Chest Front-Back

        [Header("머리")]
        [Range(-1f, 1f)] public float headNod         = 0f;  // 12 = Head Down-Up
        [Range(-1f, 1f)] public float headTurn        = 0f;  // 14 = Head Turn Left-Right

        [Header("왼팔")]
        [Range(-1f, 1f)] public float leftArmUpDown   = 0f;  // 39 = Left Arm Down-Up
        [Range(-1f, 1f)] public float leftArmFrontBack= 0f;  // 40 = Left Arm Front-Back
        [Range(-1f, 1f)] public float leftForearm     = 0f;  // 42 = Left Forearm Stretch

        [Header("오른팔")]
        [Range(-1f, 1f)] public float rightArmUpDown  = 0f;  // 48 = Right Arm Down-Up
        [Range(-1f, 1f)] public float rightArmFrontBack=0f;  // 49 = Right Arm Front-Back
        [Range(-1f, 1f)] public float rightForearm    = 0f;  // 51 = Right Forearm Stretch

        [Header("다리")]
        [Range(-1f, 1f)] public float leftKnee        = 0f;  // 24
        [Range(-1f, 1f)] public float rightKnee       = 0f;  // 32

        Animator         _animator;
        HumanPoseHandler _handler;
        HumanPose        _pose;

        void Awake()
        {
            _animator = GetComponent<Animator>();
            _handler  = new HumanPoseHandler(_animator.avatar, _animator.transform);
        }

        void LateUpdate()
        {
            if (!enabled) return;

            _handler.GetHumanPose(ref _pose);
            /*
            Set(0,  spineFrontBack);
            Set(1,  spineSideways);
            Set(3,  chestFrontBack);
            Set(12, headNod);
            Set(14, headTurn);
            Set(39, leftArmUpDown);
            Set(40, leftArmFrontBack);
            Set(42, leftForearm);
            Set(48, rightArmUpDown);
            Set(49, rightArmFrontBack);
            Set(51, rightForearm);
            Set(24, leftKnee);
            Set(32, rightKnee);
            */

            Set(6, 1);
            _handler.SetHumanPose(ref _pose);
        }

        void Set(int idx, float val)
        {
            if (idx < _pose.muscles.Length)
                _pose.muscles[idx] = val;
        }

        void OnDestroy() => _handler?.Dispose();
    }
}
