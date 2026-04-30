using UnityEngine;

namespace Parable.Motion
{
    /// <summary>
    /// 합성 모션 소스 — 실제 모캡 없이 파이프라인 테스트용.
    ///
    /// 생성하는 모션:
    ///   팔별려뛰기 패턴 — 양팔이 차렷(아래) ↔ 만세(위) 를 반복.
    ///   하체는 건드리지 않음 (UpperBody 마스크 사용).
    ///
    /// Noise ON 시:
    ///   실제 모캡 노이즈 시뮬레이션 → Cleanup 전/후 비교 가능.
    ///
    /// 확인된 muscle 인덱스 (MuscleDebugger 실측):
    ///   [37] Left Shoulder Down-Up   [46] Right Shoulder Down-Up
    ///   [39] Left Arm Down-Up        [48] Right Arm Down-Up
    ///   [42] Left Forearm Stretch    [51] Right Forearm Stretch
    /// </summary>
    public class SyntheticMotionSource : MonoBehaviour
    {
        [Header("파이프라인 연결")]
        public HumanoidPipelineStage nextStage;

        [Header("팔별려뛰기 모션")]
        [Range(0.1f, 2f)]
        [Tooltip("왕복 속도 (Hz). 1 = 1초에 한 번 올렸다 내림")]
        public float frequency = 0.6f;

        [Range(0f, 1f)]
        [Tooltip("팔이 올라가는 최대 높이 (1 = 머리 위)")]
        public float maxRaise = 1.0f;

        [Range(-1f, 0f)]
        [Tooltip("팔이 내려가는 최저 위치 (-1 = 완전 아래)")]
        public float minRaise = -0.8f;

        [Header("노이즈 (Cleanup 효과 검증용)")]
        public bool addNoise = false;

        [Range(0f, 0.3f)]
        [Tooltip("노이즈 강도. 0.05 = 실제 MediaPipe 수준, 0.15 = 심한 jitter")]
        public float noiseAmount = 0.08f;

        [Range(0f, 0.05f)]
        [Tooltip("아웃라이어 발생 확률 (마커 가림 시뮬레이션)")]
        public float outlierProbability = 0.01f;

        // ── muscle 인덱스 (Unity 2022 실측 확인) ────────────────────
        const int L_SHOULDER = 37;
        const int L_ARM_UP   = 39;
        const int L_FOREARM  = 42;
        const int R_SHOULDER = 46;
        const int R_ARM_UP   = 48;
        const int R_FOREARM  = 51;

        // ── 내부 ────────────────────────────────────────────────────
        [System.NonSerialized] HumanoidPoseData _pose;

        void Awake()
        {
            _pose = new HumanoidPoseData();
        }

        void LateUpdate()
        {
            if (_pose == null) return;
            BuildPose();
            nextStage?.Receive(_pose);
        }

        void BuildPose()
        {
            System.Array.Clear(_pose.muscles, 0, _pose.muscles.Length);

            // 0→1→0 사인 파형 (0=아래, 1=위)
            float t       = Time.time;
            float cycle   = (Mathf.Sin(t * frequency * 2f * Mathf.PI) + 1f) * 0.5f;
            float armPos  = Mathf.Lerp(minRaise, maxRaise, cycle);

            // 양팔 동시에 올렸다 내리기
            _pose.muscles[L_SHOULDER] = armPos * 0.4f;  // 어깨도 함께 올라가야 자연스러움
            _pose.muscles[R_SHOULDER] = armPos * 0.4f;
            _pose.muscles[L_ARM_UP]   = armPos;
            _pose.muscles[R_ARM_UP]   = armPos;
            _pose.muscles[L_FOREARM]  = 1f;             // 팔꿈치 쭉 펴기 (+1 = 완전 신전)
            _pose.muscles[R_FOREARM]  = 1f;

            _pose.bodyPosition = Vector3.up;
            _pose.bodyRotation = Quaternion.identity;
            _pose.timestamp    = t;
            _pose.isValid      = true;
            _pose.activeMask   = PoseMaskFlags.UpperBody;

            if (addNoise)
                InjectNoise();
        }

        void InjectNoise()
        {
            for (int i = 0; i < _pose.muscles.Length; i++)
            {
                float noise = (Random.value + Random.value - 1f) * noiseAmount;
                _pose.muscles[i] = Mathf.Clamp(_pose.muscles[i] + noise, -1f, 1f);

                if (outlierProbability > 0f && Random.value < outlierProbability)
                    _pose.muscles[i] = Random.value > 0.5f ? 1f : -1f;
            }
        }

#if UNITY_EDITOR
        void OnDrawGizmosSelected()
        {
            string noise = addNoise ? $"Noise {noiseAmount:F2}" : "Clean";
            UnityEditor.Handles.Label(
                transform.position + Vector3.up * 2.2f,
                $"[SyntheticSource] {frequency:F1}Hz  {noise}");
        }
#endif
    }
}
