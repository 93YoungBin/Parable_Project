using System.IO;
using UnityEditor;
using UnityEngine;

namespace Parable.Motion.Editor
{
    /// <summary>
    /// 합성 모션 데이터를 .pmocap 파일로 굽는 에디터 도구.
    /// 메뉴: Parable > Bake Synthetic Arm Motion
    ///
    /// 생성되는 데이터:
    ///   팔별려뛰기 패턴 (SyntheticMotionSource와 동일한 공식) + 노이즈 포함
    ///   → 두 아바타가 완전히 동일한 입력 데이터를 공유 가능
    /// </summary>
    public static class SyntheticMotionBaker
    {
        // ── 베이크 파라미터 ────────────────────────────────────────────
        const float DURATION      = 8f;     // 초
        const float FPS           = 30f;
        const float FREQUENCY     = 0.6f;   // Hz (SyntheticMotionSource 기본값과 동일)
        const float MAX_RAISE     = 1.0f;
        const float MIN_RAISE     = -0.8f;
        const float NOISE_AMOUNT  = 0.02f;
        const float OUTLIER_PROB  = 0.003f;
        const int   RANDOM_SEED   = 42;     // 재현 가능한 노이즈

        // muscle 인덱스 (MuscleDebugger 실측)
        const int L_SHOULDER = 37;
        const int L_ARM_UP   = 39;
        const int L_FOREARM  = 42;
        const int R_SHOULDER = 46;
        const int R_ARM_UP   = 48;
        const int R_FOREARM  = 51;

        [MenuItem("Parable/Bake Synthetic Arm Motion")]
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

                float[] m = new float[muscleCount];

                // ── 팔별려뛰기 공식 ───────────────────────────────────
                float cycle  = (Mathf.Sin(t * FREQUENCY * 2f * Mathf.PI) + 1f) * 0.5f;
                float armPos = Mathf.Lerp(MIN_RAISE, MAX_RAISE, cycle);

                m[L_SHOULDER] = armPos * 0.4f;
                m[R_SHOULDER] = armPos * 0.4f;
                m[L_ARM_UP]   = armPos;
                m[R_ARM_UP]   = armPos;
                m[L_FOREARM]  = 1f;
                m[R_FOREARM]  = 1f;

                // ── 노이즈 주입 ───────────────────────────────────────
                for (int i = 0; i < muscleCount; i++)
                {
                    float noise = ((float)rng.NextDouble() + (float)rng.NextDouble() - 1f)
                                  * NOISE_AMOUNT;
                    m[i] = Mathf.Clamp(m[i] + noise, -1f, 1f);

                    if (rng.NextDouble() < OUTLIER_PROB)
                        m[i] = rng.NextDouble() > 0.5 ? 1f : -1f;
                }

                muscles[f]   = m;
                positions[f] = Vector3.up;
                rotations[f] = Quaternion.identity;
            }

            // ── .pmocap 저장 ──────────────────────────────────────────
            string dir  = Path.GetFullPath(
                Path.Combine(Application.dataPath, "..", "Recordings"));
            Directory.CreateDirectory(dir);
            string path = Path.Combine(dir, "synthetic_arms_noisy.pmocap");

            using (var w = new BinaryWriter(File.Open(path, FileMode.Create)))
            {
                w.Write("PMOCAP\0\0".ToCharArray());  // magic 8B
                w.Write(1);                            // version
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

            Debug.Log($"[SyntheticMotionBaker] 저장 완료 ({frameCount} 프레임, {DURATION}초)\n{path}");
            EditorUtility.RevealInFinder(path);
        }
    }
}
