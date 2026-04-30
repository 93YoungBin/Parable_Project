using System;
using UnityEngine;

namespace Parable.Motion
{
    /// <summary>
    /// 모션 파이프라인의 표준화된 중간 포맷.
    /// 소스(mocap/animation) → 클린업 → 리타겟팅 → IK 모든 단계가 이 타입으로 통신.
    ///
    /// Unity HumanPose 래퍼:
    ///   muscles[0..94] — HumanTrait.MuscleName 인덱스 기준 근육값 (-1~1)
    ///   bodyPosition/Rotation — 루트 월드 트랜스폼
    ///
    /// 왜 HumanPose를 그대로 안 쓰나:
    ///   HumanPose는 struct라 복사 비용이 있고 타임스탬프/마스크 정보를 담을 수 없음.
    ///   이 래퍼는 파이프라인 메타데이터(시간, 마스크, 유효성)를 함께 전달.
    /// </summary>
    // 런타임 전용 — 직렬화 대상 아님
    public class HumanoidPoseData
    {
        // ── Muscle 데이터 (Unity HumanTrait 기준 95개) ────────────────
        public float[] muscles;

        // ── 루트 트랜스폼 ─────────────────────────────────────────────
        public Vector3    bodyPosition = Vector3.zero;
        public Quaternion bodyRotation = Quaternion.identity;

        // ── 파이프라인 메타데이터 ──────────────────────────────────────
        public float timestamp;          // 샘플링 시각 (Time.time)
        public bool  isValid = true;     // 클린업 단계에서 false 마킹 가능
        public PoseMaskFlags activeMask = PoseMaskFlags.FullBody;

        // ── 복사 생성자 ──────────────────────────────────────────────
        public HumanoidPoseData()
        {
            muscles = new float[HumanTrait.MuscleCount];
        }

        public HumanoidPoseData(HumanoidPoseData src)
        {
            muscles = new float[HumanTrait.MuscleCount];
            Array.Copy(src.muscles, muscles, muscles.Length);
            bodyPosition = src.bodyPosition;
            bodyRotation = src.bodyRotation;
            timestamp    = src.timestamp;
            isValid      = src.isValid;
            activeMask   = src.activeMask;
        }

        // ── HumanPose 변환 ────────────────────────────────────────────
        public HumanPose ToHumanPose()
        {
            var p = new HumanPose();
            p.bodyPosition = bodyPosition;
            p.bodyRotation = bodyRotation;
            p.muscles      = (float[])muscles.Clone();
            return p;
        }

        public static HumanoidPoseData FromHumanPose(HumanPose p)
        {
            var d = new HumanoidPoseData();
            d.bodyPosition = p.bodyPosition;
            d.bodyRotation = p.bodyRotation;
            Array.Copy(p.muscles, d.muscles,
                Mathf.Min(p.muscles.Length, d.muscles.Length));
            d.timestamp = Time.time;
            return d;
        }

        // ── 마스크 기반 muscle 범위 ───────────────────────────────────
        // Unity HumanTrait.MuscleName 실제 인덱스 (Unity 2022 LTS 기준, MuscleDebugger 실측 확인)
        //  0-20 : Body  — Spine, Chest, UpperChest, Neck, Head, Eyes, Jaw
        // 21-28 : LeftLeg  — Left UpperLeg(3) + LowerLeg(2) + Foot(2) + Toes(1)
        // 29-36 : RightLeg — Right UpperLeg(3) + LowerLeg(2) + Foot(2) + Toes(1)
        // 37-45 : LeftArm  — Left Shoulder(2) + Arm(3) + Forearm(2) + Hand(2)
        // 46-54 : RightArm — Right Shoulder(2) + Arm(3) + Forearm(2) + Hand(2)
        // 55-94 : Fingers  — Left(20) + Right(20)
        public void ApplyMask(PoseMaskFlags mask, HumanoidPoseData source)
        {
            if (mask.HasFlag(PoseMaskFlags.Body))
                CopyRange(source, 0, 20);
            if (mask.HasFlag(PoseMaskFlags.LeftLeg))
                CopyRange(source, 21, 28);
            if (mask.HasFlag(PoseMaskFlags.RightLeg))
                CopyRange(source, 29, 36);
            if (mask.HasFlag(PoseMaskFlags.LeftArm))
                CopyRange(source, 37, 45);
            if (mask.HasFlag(PoseMaskFlags.RightArm))
                CopyRange(source, 46, 54);
            if (mask.HasFlag(PoseMaskFlags.Face))
                CopyRange(source, 55, 94);
        }

        void CopyRange(HumanoidPoseData src, int from, int to)
        {
            int len = Mathf.Min(to, muscles.Length - 1);
            for (int i = from; i <= len; i++)
                muscles[i] = src.muscles[i];
        }
    }

    /// <summary>어느 신체 부위 muscle을 활성화할지 지정하는 플래그.</summary>
    [Flags]
    public enum PoseMaskFlags
    {
        None      = 0,
        Body      = 1 << 0,   // Spine / Chest / Neck / Head (0-20)
        LeftArm   = 1 << 1,   // Left Shoulder ~ Hand (37-45)
        RightArm  = 1 << 2,   // Right Shoulder ~ Hand (46-54)
        LeftLeg   = 1 << 3,   // Left UpperLeg ~ Toes (21-28)
        RightLeg  = 1 << 4,   // Right UpperLeg ~ Toes (29-36)
        Face      = 1 << 5,   // Fingers (55-94)
        Arms      = LeftArm | RightArm,               // 팔만 (척추 미포함)
        UpperBody = Body | LeftArm | RightArm,
        LowerBody = LeftLeg | RightLeg,
        FullBody  = Body | LeftArm | RightArm | LeftLeg | RightLeg | Face,
    }
}