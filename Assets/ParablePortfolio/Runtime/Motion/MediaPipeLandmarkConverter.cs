using UnityEngine;

namespace Parable.Motion
{
    /// <summary>
    /// MediaPipe world landmarks (33개) → Unity HumanPose.muscles[] 변환기.
    ///
    /// 입력: MediaPipePoseTracker.TryGetWorldLandmarks() 결과
    ///   - Unity 좌표계로 변환된 Vector3[33]
    ///   - 힙 중심 기준, 단위 미터
    ///
    /// 출력: HumanPose.muscles[] 직접 수정 (in-place)
    ///   - 신체 분절의 관절 각도 → muscle 값 (-1 ~ 1)
    ///
    /// 변환 전략:
    ///   1. 힙/어깨 기준 토르소 프레임 계산
    ///   2. 각 분절 벡터를 토르소 로컬 좌표로 분해
    ///   3. 각도 → muscle 값 매핑 (경험적 범위 사용)
    ///
    /// 주의:
    ///   - MediaPipe는 손가락·발가락 랜드마크를 제공하지 않음
    ///   - 손목/발목 이하 muscle은 변환하지 않음
    ///   - 변환 정밀도보다 실시간 안정성 우선 (프로토타입 레벨)
    /// </summary>
    public static class MediaPipeLandmarkConverter
    {
        // ── MediaPipe Pose 랜드마크 인덱스 ──────────────────────────
        // https://ai.google.dev/edge/mediapipe/solutions/vision/pose_landmarker
        const int IDX_NOSE          = 0;
        const int IDX_L_SHOULDER    = 11;
        const int IDX_R_SHOULDER    = 12;
        const int IDX_L_ELBOW       = 13;
        const int IDX_R_ELBOW       = 14;
        const int IDX_L_WRIST       = 15;
        const int IDX_R_WRIST       = 16;
        const int IDX_L_HIP         = 23;
        const int IDX_R_HIP         = 24;
        const int IDX_L_KNEE        = 25;
        const int IDX_R_KNEE        = 26;
        const int IDX_L_ANKLE       = 27;
        const int IDX_R_ANKLE       = 28;

        // ── HumanTrait muscle 인덱스 (Unity 공식 순서) ──────────────
        // 실측 확인: HumanTrait.MuscleName[] 배열
        const int M_SPINE_FB        = 0;   // Spine Front-Back
        const int M_SPINE_LR        = 1;   // Spine Left-Right
        const int M_CHEST_FB        = 3;   // Chest Front-Back
        const int M_CHEST_LR        = 4;   // Chest Left-Right
        const int M_NECK_UD         = 9;   // Neck Nod Down-Up
        const int M_HEAD_UD         = 12;  // Head Nod Down-Up
        const int M_HEAD_LR         = 14;  // Head Turn Left-Right

        const int M_L_LEG_FB        = 21;  // Left Upper Leg Front-Back
        const int M_L_LEG_IO        = 22;  // Left Upper Leg In-Out
        const int M_L_KNEE          = 24;  // Left Lower Leg Stretch
        const int M_L_FOOT_UD       = 26;  // Left Foot Up-Down

        const int M_R_LEG_FB        = 29;  // Right Upper Leg Front-Back
        const int M_R_LEG_IO        = 30;  // Right Upper Leg In-Out
        const int M_R_KNEE          = 32;  // Right Lower Leg Stretch
        const int M_R_FOOT_UD       = 34;  // Right Foot Up-Down

        const int M_L_SHOULDER_UD   = 37;  // Left Shoulder Down-Up
        const int M_L_SHOULDER_FB   = 38;  // Left Shoulder Front-Back
        const int M_L_ARM_UD        = 39;  // Left Arm Down-Up
        const int M_L_ARM_FB        = 40;  // Left Arm Front-Back
        const int M_L_FOREARM       = 42;  // Left Forearm Stretch

        const int M_R_SHOULDER_UD   = 46;  // Right Shoulder Down-Up
        const int M_R_SHOULDER_FB   = 47;  // Right Shoulder Front-Back
        const int M_R_ARM_UD        = 48;  // Right Arm Down-Up
        const int M_R_ARM_FB        = 49;  // Right Arm Front-Back
        const int M_R_FOREARM       = 51;  // Right Forearm Stretch

        // ── 가시성 임계값 ────────────────────────────────────────────
        [Tooltip("이 값 이하의 가시성인 랜드마크는 변환에서 제외")]
        public static float visibilityThreshold = 0.3f;

        // ── 각도 → muscle 매핑 범위 (도 단위) ───────────────────────
        // HumanPose muscle 1.0 = 최대 범위, 0 = 기본 포즈, -1 = 최소 범위
        // 아래는 경험적으로 조정된 값 (아바타/리그에 따라 다를 수 있음)
        const float SPINE_FB_RANGE    = 35f;   // ±35° 전후 기울기
        const float SPINE_LR_RANGE    = 25f;   // ±25° 좌우 기울기
        const float ARM_UD_RANGE_UP   = 90f;   // 팔 올리기 90° = muscle 1
        const float ARM_UD_RANGE_DOWN = 45f;   // 팔 내리기 45° = muscle -1
        const float ARM_FB_RANGE      = 90f;   // ±90° 전후
        const float FOREARM_RANGE     = 150f;  // 팔꿈치 0°(펼침) ~ 150°(굽힘)
        const float LEG_FB_RANGE      = 90f;   // ±90° 전후 (고관절 굴곡)
        const float LEG_IO_RANGE      = 45f;   // ±45° 내외전
        const float KNEE_RANGE        = 150f;  // 무릎 0°(펼침) ~ 150°(굽힘)

        /// <summary>
        /// 랜드마크 배열을 HumanPose muscles에 적용.
        /// MediaPipePoseTracker.TryGetWorldLandmarks() 직후 LateUpdate에서 호출.
        /// </summary>
        public static void Apply(Vector3[] lm, float[] vis, ref HumanPose pose)
        {
            if (lm == null || lm.Length < MediaPipePoseTracker.LANDMARK_COUNT) return;
            if (pose.muscles == null) return;

            // ── 토르소 프레임 계산 ──────────────────────────────────
            var lhip   = lm[IDX_L_HIP];
            var rhip   = lm[IDX_R_HIP];
            var lshldr = lm[IDX_L_SHOULDER];
            var rshldr = lm[IDX_R_SHOULDER];

            var hipMid      = (lhip + rhip) * 0.5f;
            var shoulderMid = (lshldr + rshldr) * 0.5f;

            // 토르소 로컬 축
            // torsoUp: 힙 → 어깨. 힙이 불안정하면 world up 폴백
            var torsoUp = (shoulderMid - hipMid);
            torsoUp = torsoUp.sqrMagnitude > 0.0001f ? torsoUp.normalized : Vector3.up;

            // torsoRight: 어깨 기준 우선, 힙 보조
            // 어깨는 보통 잘 잡히므로 더 안정적
            var shoulderRight = rshldr - lshldr;
            var hipRight      = rhip   - lhip;
            var torsoRight    = shoulderRight.sqrMagnitude > 0.001f
                ? shoulderRight.normalized
                : (hipRight.sqrMagnitude > 0.001f ? hipRight.normalized : Vector3.right);

            // torsoForward: 외적
            var torsoForward = Vector3.Cross(torsoUp, torsoRight).normalized;
            if (torsoForward.sqrMagnitude < 0.001f) torsoForward = Vector3.forward;

            // ── 척추/가슴 ───────────────────────────────────────────
            ConvertSpine(torsoUp, torsoRight, torsoForward, ref pose);

            // ── 머리 ────────────────────────────────────────────────
            ConvertHead(lm, vis, shoulderMid, torsoUp, torsoRight, torsoForward, ref pose);

            // ── 팔 ──────────────────────────────────────────────────
            ConvertArm(
                lm[IDX_L_SHOULDER], lm[IDX_L_ELBOW], lm[IDX_L_WRIST],
                vis[IDX_L_SHOULDER], vis[IDX_L_ELBOW], vis[IDX_L_WRIST],
                torsoUp, torsoRight, torsoForward,
                isLeft: true, ref pose);

            ConvertArm(
                lm[IDX_R_SHOULDER], lm[IDX_R_ELBOW], lm[IDX_R_WRIST],
                vis[IDX_R_SHOULDER], vis[IDX_R_ELBOW], vis[IDX_R_WRIST],
                torsoUp, torsoRight, torsoForward,
                isLeft: false, ref pose);

            // ── 다리 ────────────────────────────────────────────────
            ConvertLeg(
                lm[IDX_L_HIP], lm[IDX_L_KNEE], lm[IDX_L_ANKLE],
                vis[IDX_L_HIP], vis[IDX_L_KNEE], vis[IDX_L_ANKLE],
                torsoUp, torsoRight, torsoForward,
                isLeft: true, ref pose);

            ConvertLeg(
                lm[IDX_R_HIP], lm[IDX_R_KNEE], lm[IDX_R_ANKLE],
                vis[IDX_R_HIP], vis[IDX_R_KNEE], vis[IDX_R_ANKLE],
                torsoUp, torsoRight, torsoForward,
                isLeft: false, ref pose);
        }

        // ── 척추 변환 ────────────────────────────────────────────────

        static void ConvertSpine(
            Vector3 torsoUp, Vector3 torsoRight, Vector3 torsoForward,
            ref HumanPose pose)
        {
            // torsoUp이 world up (0,1,0)에서 얼마나 기울어졌는지 측정
            // deviation = 기울기 벡터 (torsoForward/torsoRight 성분으로 분해)
            var deviation = torsoUp - Vector3.up;  // world up 대비 편차

            // 전후 기울기: torsoForward 방향 성분
            float tiltFB = Vector3.Dot(deviation, torsoForward) * Mathf.Rad2Deg;
            // 좌우 기울기: torsoRight 방향 성분
            float tiltLR = Vector3.Dot(deviation, torsoRight)   * Mathf.Rad2Deg;

            SetMuscle(ref pose, M_SPINE_FB,  tiltFB / SPINE_FB_RANGE);
            SetMuscle(ref pose, M_SPINE_LR,  tiltLR / SPINE_LR_RANGE);
            SetMuscle(ref pose, M_CHEST_FB,  tiltFB / SPINE_FB_RANGE * 0.6f);
            SetMuscle(ref pose, M_CHEST_LR,  tiltLR / SPINE_LR_RANGE * 0.6f);
        }

        // ── 머리 변환 ────────────────────────────────────────────────

        static void ConvertHead(
            Vector3[] lm, float[] vis,
            Vector3 shoulderMid, Vector3 torsoUp, Vector3 torsoRight, Vector3 torsoForward,
            ref HumanPose pose)
        {
            float noseVis = vis[IDX_NOSE];
            if (noseVis < visibilityThreshold) return;

            // 코 랜드마크 → 어깨 중심 대비 상대 위치
            var noseRel = lm[IDX_NOSE] - shoulderMid;

            // 고개 끄덕임: Up축 성분
            float headUD = Vector3.Dot(noseRel.normalized, torsoUp);
            // 고개 돌리기: Right축 성분
            float headLR = Vector3.Dot(noseRel.normalized, torsoRight);

            SetMuscle(ref pose, M_HEAD_UD, headUD * 0.8f);   // 0.8 = 경험적 스케일
            SetMuscle(ref pose, M_NECK_UD, headUD * 0.4f);
            SetMuscle(ref pose, M_HEAD_LR, headLR * 0.6f);
        }

        // ── 팔 변환 ──────────────────────────────────────────────────

        static void ConvertArm(
            Vector3 shoulder, Vector3 elbow, Vector3 wrist,
            float shoulderVis, float elbowVis, float wristVis,
            Vector3 torsoUp, Vector3 torsoRight, Vector3 torsoForward,
            bool isLeft, ref HumanPose pose)
        {
            int mShoulderUD = isLeft ? M_L_SHOULDER_UD : M_R_SHOULDER_UD;
            int mShoulderFB = isLeft ? M_L_SHOULDER_FB : M_R_SHOULDER_FB;
            int mArmUD      = isLeft ? M_L_ARM_UD      : M_R_ARM_UD;
            int mArmFB      = isLeft ? M_L_ARM_FB      : M_R_ARM_FB;
            int mForearm    = isLeft ? M_L_FOREARM      : M_R_FOREARM;

            // 어깨는 반드시 필요, 팔꿈치는 낮은 기준 적용
            if (shoulderVis < visibilityThreshold) return;
            if (elbowVis < visibilityThreshold * 0.6f) return; // 팔꿈치는 더 관대하게

            var upperArmDir = (elbow - shoulder);
            if (upperArmDir.sqrMagnitude < 0.0001f) return;
            upperArmDir.Normalize();

            // ── 어깨 (Shoulder Down-Up / Front-Back) ──────────────
            // 어깨 muscle은 쇄골 움직임 (소폭 보정)
            // 팔이 위로 올라갈수록 어깨도 따라 올라가는 경향
            float armY = Vector3.Dot(upperArmDir, torsoUp);
            float armZ = Vector3.Dot(upperArmDir, torsoForward);

            // 어깨 Down-Up: 팔이 위에 있을 때 어깨가 올라감
            float shoulderUD = Mathf.Clamp(armY - 0.5f, -0.5f, 0.5f); // 45° 이상에서 반응
            SetMuscle(ref pose, mShoulderUD, shoulderUD);
            // SetMuscle(ref pose, mShoulderFB, armZ * 0.3f); // 임시 비활성화

            // ── 상완 (Arm Down-Up / Front-Back) ───────────────────
            // armY: -1(완전 아래) ~ 0(수평) ~ 1(완전 위)
            // muscle 범위가 보통 수평 기준 비대칭이므로 위/아래 스케일 분리
            float armUD = armY >= 0f
                ? armY                                           // 위: 그대로
                : armY * (ARM_UD_RANGE_UP / ARM_UD_RANGE_DOWN); // 아래: 범위 보정

            SetMuscle(ref pose, mArmUD, armUD * 1.5f);
            // armFB: torso 프레임 방향 이슈로 임시 비활성화 (교차 방지)
            // SetMuscle(ref pose, mArmFB, -armZ * 1.5f);

            // ── 전완 (Forearm Stretch = 팔꿈치 굽힘) ──────────────
            if (wristVis < visibilityThreshold) return;

            var lowerArmDir = (wrist - elbow);
            if (lowerArmDir.sqrMagnitude < 0.0001f) return;
            lowerArmDir.Normalize();

            // 팔꿈치 각도: upperArmDir 방향과 lowerArmDir의 연속 방향 사이 각도
            // 완전히 펼쳐지면 (straight) dot = 1, 직각 = 0, 완전히 굽혀지면 = -1
            float cosBend = Vector3.Dot(upperArmDir, lowerArmDir);
            // muscle: 1 = 완전히 펼침(straight), -1 = 완전히 굽힘
            // 실제로 Unity Forearm Stretch: 1 = fully stretched, -1 = fully bent
            float bendAngle = Mathf.Acos(Mathf.Clamp(cosBend, -1f, 1f)) * Mathf.Rad2Deg;
            float forearmMuscle = 1f - (bendAngle / FOREARM_RANGE) * 2f; // 0°→1, 75°→0, 150°→-1
            SetMuscle(ref pose, mForearm, forearmMuscle);
        }

        // ── 다리 변환 ────────────────────────────────────────────────

        static void ConvertLeg(
            Vector3 hip, Vector3 knee, Vector3 ankle,
            float hipVis, float kneeVis, float ankleVis,
            Vector3 torsoUp, Vector3 torsoRight, Vector3 torsoForward,
            bool isLeft, ref HumanPose pose)
        {
            int mLegFB  = isLeft ? M_L_LEG_FB  : M_R_LEG_FB;
            int mLegIO  = isLeft ? M_L_LEG_IO  : M_R_LEG_IO;
            int mKnee   = isLeft ? M_L_KNEE     : M_R_KNEE;
            int mFootUD = isLeft ? M_L_FOOT_UD  : M_R_FOOT_UD;

            if (hipVis < visibilityThreshold || kneeVis < visibilityThreshold) return;

            var upperLegDir = (knee - hip);
            if (upperLegDir.sqrMagnitude < 0.0001f) return;
            upperLegDir.Normalize();

            // 고관절 굴곡/신전 (Front-Back)
            // 다리가 앞으로 올라가면 양수, 뒤로 가면 음수
            float legFB = Vector3.Dot(upperLegDir, torsoForward);
            float legFBMuscle = legFB * (ARM_UD_RANGE_UP / LEG_FB_RANGE);
            SetMuscle(ref pose, mLegFB, legFBMuscle);

            // 고관절 내외전 (In-Out)
            float sideSign = isLeft ? -1f : 1f;
            float legIO    = Vector3.Dot(upperLegDir, torsoRight) * sideSign;
            SetMuscle(ref pose, mLegIO, legIO * (90f / LEG_IO_RANGE));

            // 무릎 굴곡 (Lower Leg Stretch)
            if (kneeVis < visibilityThreshold || ankleVis < visibilityThreshold) return;

            var lowerLegDir = (ankle - knee);
            if (lowerLegDir.sqrMagnitude < 0.0001f) return;
            lowerLegDir.Normalize();

            float cosKnee    = Vector3.Dot(upperLegDir, lowerLegDir);
            float kneeAngle  = Mathf.Acos(Mathf.Clamp(cosKnee, -1f, 1f)) * Mathf.Rad2Deg;
            // muscle: 1 = 완전히 펼침(straight), -1 = 완전히 굽힘
            float kneeMuscle = 1f - (kneeAngle / KNEE_RANGE) * 2f;
            SetMuscle(ref pose, mKnee, kneeMuscle);
        }

        // ── 헬퍼 ─────────────────────────────────────────────────────

        static void SetMuscle(ref HumanPose pose, int muscleIdx, float value)
        {
            if (muscleIdx < 0 || muscleIdx >= pose.muscles.Length) return;
            pose.muscles[muscleIdx] = Mathf.Clamp(value, -1f, 1f);
        }
    }
}
