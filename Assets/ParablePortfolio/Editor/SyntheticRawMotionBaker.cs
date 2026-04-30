using System.IO;
using UnityEditor;
using UnityEngine;

namespace Parable.Motion.Editor
{
    /// <summary>
    /// 정규화 전 raw 모션 데이터를 .pmocap 파일로 굽는 에디터 도구.
    /// 메뉴: Parable > Bake Raw Arm Motion (Degrees)
    ///
    /// 저장되는 값:
    ///   muscle 배열에 HumanTrait -1~1 정규화 값이 아닌
    ///   관절 각도(degrees)를 그대로 저장.
    ///   → 파이프라인에서 NormalizationStage가 -1~1로 변환하는 과정을 시연.
    ///
    /// 포맷: version = 2 (raw degrees, v1과 구별)
    /// </summary>
    public static class SyntheticRawMotionBaker
    {
        const float DURATION      = 8f;
        const float FPS           = 30f;
        const float FREQUENCY     = 0.6f;   // Hz

        // 관절 각도 범위 (degrees)
        // 팔 올림(+) / 내림(-) 기준 — 해부학적 가동 범위 내 대표값
        const float ARM_UP_DEG    =  80f;   // 팔 최대 올림 (~정수리 방향)
        const float ARM_DOWN_DEG  = -65f;   // 팔 최대 내림
        const float SHOULDER_SCALE = 0.4f;  // 어깨는 팔의 40%만 올라감
        const float FOREARM_DEG   = 120f;   // 팔꿈치 신전 (0=굽힘, 140=완전 신전)

        // 노이즈 — degree 단위이므로 muscle 단위(0.02)보다 큰 값 사용
        const float NOISE_DEGREES = 8f;
        const float OUTLIER_PROB  = 0f;    // 아웃라이어 제거 — 노이즈(±8°)만으로 필터링 효과 시연
        const int   RANDOM_SEED   = 42;

        // muscle 인덱스 (MuscleDebugger 실측)
        const int L_SHOULDER = 37;
        const int L_ARM_UP   = 39;
        const int L_FOREARM  = 42;
        const int R_SHOULDER = 46;
        const int R_ARM_UP   = 48;
        const int R_FOREARM  = 51;

        [MenuItem("Parable/Bake Raw Arm Motion (Degrees)")]
        public static void Bake()
        {
            int muscleCount = HumanTrait.MuscleCount;
            int frameCount  = Mathf.RoundToInt(DURATION * FPS);
            float dt        = 1f / FPS;

            var muscles   = new float[frameCount][];
            var positions = new Vector3[frameCount];
            var rotations = new Quaternion[frameCount];

            var rng = new System.Random(RANDOM_SEED);

            for (int f = 0; f < frameCount; f++)
            {
                float t = f * dt;
                float[] m = new float[muscleCount];  // 기본값 0° (모든 관절 중립)

                // ── 팔별려뛰기 — degree 단위 ──────────────────────────
                float cycle  = (Mathf.Sin(t * FREQUENCY * 2f * Mathf.PI) + 1f) * 0.5f;
                float armDeg = Mathf.Lerp(ARM_DOWN_DEG, ARM_UP_DEG, cycle);

                m[L_SHOULDER] = armDeg * SHOULDER_SCALE;
                m[R_SHOULDER] = armDeg * SHOULDER_SCALE;
                m[L_ARM_UP]   = armDeg;
                m[R_ARM_UP]   = armDeg;
                m[L_FOREARM]  = FOREARM_DEG;
                m[R_FOREARM]  = FOREARM_DEG;

                // ── 노이즈 (degree 스케일) ────────────────────────────
                for (int i = 0; i < muscleCount; i++)
                {
                    float noise = ((float)rng.NextDouble() + (float)rng.NextDouble() - 1f)
                                  * NOISE_DEGREES;
                    m[i] += noise;

                    // 아웃라이어: 해부학적 극단값 (±180°) 주입
                    if (rng.NextDouble() < OUTLIER_PROB)
                        m[i] = rng.NextDouble() > 0.5 ? 180f : -180f;
                }

                muscles[f]   = m;
                positions[f] = Vector3.up;
                rotations[f] = Quaternion.identity;
            }

            // ── 저장 ──────────────────────────────────────────────────
            string dir  = Path.GetFullPath(
                Path.Combine(Application.dataPath, "..", "Recordings"));
            Directory.CreateDirectory(dir);
            string path = Path.Combine(dir, "raw_arms_degrees.pmocap");

            using (var w = new BinaryWriter(File.Open(path, FileMode.Create)))
            {
                w.Write("PMOCAP\0\0".ToCharArray());
                w.Write(2);              // version 2 = raw degrees
                w.Write(FPS);
                w.Write(frameCount);
                w.Write(muscleCount);

                for (int f = 0; f < frameCount; f++)
                {
                    foreach (float v in muscles[f]) w.Write(v);
                    w.Write(positions[f].x); w.Write(positions[f].y); w.Write(positions[f].z);
                    w.Write(rotations[f].x); w.Write(rotations[f].y);
                    w.Write(rotations[f].z); w.Write(rotations[f].w);
                }
            }

            Debug.Log($"[RawMotionBaker] 저장 완료 — raw degrees ({frameCount}프레임)\n{path}");
            EditorUtility.RevealInFinder(path);
        }
    }
}
