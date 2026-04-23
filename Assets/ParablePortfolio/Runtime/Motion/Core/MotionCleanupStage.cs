using UnityEngine;

namespace Parable.Motion
{
    /// <summary>
    /// 실시간 모션 클린업 파이프라인 스테이지 — T-2.2 핵심.
    ///
    /// 처리 순서 (Update 매 프레임):
    ///   1. Jitter 감지: muscle velocity > jitterThreshold → isJitter 플래그
    ///   2. One-Euro Filter: 95개 muscle 각각에 적용
    ///   3. 루트 이동 분리 필터: bodyPosition / bodyRotation 별도 처리
    ///   4. isValid = false 프레임 스킵 (마커 가림 등)
    ///
    /// 시행착오 메모:
    ///   단순 EMA(Lerp)는 빠른 동작에서 lag 발생 →
    ///   One-Euro Filter의 adaptive cutoff로 해결.
    /// </summary>
    public class MotionCleanupStage : HumanoidPipelineStage
    {
        [Header("One-Euro Filter — Muscle")]
        [Range(0.1f, 5f)]  public float muscleMinCutoff = 1.0f;
        [Range(0f,   1f)]  public float muscleBeta      = 0.1f;

        [Header("One-Euro Filter — Root Position")]
        [Range(0.1f, 5f)]  public float rootPosMinCutoff = 2.0f;
        [Range(0f,   1f)]  public float rootPosBeta      = 0.3f;

        [Header("Jitter 감지")]
        [Tooltip("프레임 간 muscle 변화 속도 임계값. 초과 시 jitter 판정 후 필터 강화")]
        [Range(0f, 50f)]   public float jitterThreshold  = 15f;
        [Tooltip("jitter 판정 muscle 비율 (0~1). 이 이상이면 프레임 전체 무효 처리")]
        [Range(0f, 1f)]    public float invalidFrameRatio = 0.5f;

        [Header("Debug")]
        public bool showJitterDebug = false;

        // 95개 muscle 필터
        OneEuroFilter[] _muscleFilters;

        // 루트 XYZ 별도 필터
        OneEuroFilter _rootPosX, _rootPosY, _rootPosZ;
        OneEuroFilter _rootRotX, _rootRotY, _rootRotZ, _rootRotW;

        // Jitter 통계 (에디터 디버그용)
        [System.NonSerialized] public int  LastJitterCount;
        [System.NonSerialized] public bool LastFrameInvalid;

        float[] _prevMuscles;

        void Awake() => Initialize();

        void Initialize()
        {
            int count = UnityEngine.HumanTrait.MuscleCount;
            _muscleFilters = new OneEuroFilter[count];
            _prevMuscles   = new float[count];

            for (int i = 0; i < count; i++)
                _muscleFilters[i] = new OneEuroFilter(muscleMinCutoff, muscleBeta);

            _rootPosX = new OneEuroFilter(rootPosMinCutoff, rootPosBeta);
            _rootPosY = new OneEuroFilter(rootPosMinCutoff, rootPosBeta);
            _rootPosZ = new OneEuroFilter(rootPosMinCutoff, rootPosBeta);
            _rootRotX = new OneEuroFilter(rootPosMinCutoff, rootPosBeta);
            _rootRotY = new OneEuroFilter(rootPosMinCutoff, rootPosBeta);
            _rootRotZ = new OneEuroFilter(rootPosMinCutoff, rootPosBeta);
            _rootRotW = new OneEuroFilter(rootPosMinCutoff, rootPosBeta);
        }

        protected override HumanoidPoseData Process(HumanoidPoseData input)
        {
            // 유효하지 않은 포즈 스킵
            if (!input.isValid) return null;

            float dt = Time.deltaTime;
            if (dt <= 0f) return input;

            // ── 1. Jitter 감지 ───────────────────────────────────────
            int jitterCount = 0;
            for (int i = 0; i < input.muscles.Length; i++)
            {
                float velocity = Mathf.Abs(input.muscles[i] - _prevMuscles[i]) / dt;
                if (velocity > jitterThreshold)
                    jitterCount++;
            }

            LastJitterCount   = jitterCount;
            float jitterRatio = (float)jitterCount / input.muscles.Length;

            // jitter 비율이 임계 초과 → 프레임 무효화
            if (jitterRatio >= invalidFrameRatio)
            {
                LastFrameInvalid = true;
                if (showJitterDebug)
                    Debug.LogWarning(
                        $"[Cleanup] Invalid frame: jitter {jitterRatio:P0} " +
                        $">= threshold {invalidFrameRatio:P0}");
                return null;
            }
            LastFrameInvalid = false;

            // ── 2. One-Euro Filter 적용 ──────────────────────────────
            var output = new HumanoidPoseData(input);

            for (int i = 0; i < output.muscles.Length; i++)
            {
                output.muscles[i] = _muscleFilters[i].Filter(input.muscles[i], dt);
                _prevMuscles[i]   = input.muscles[i];
            }

            // ── 3. 루트 트랜스폼 필터 ────────────────────────────────
            var pos = input.bodyPosition;
            output.bodyPosition = new Vector3(
                _rootPosX.Filter(pos.x, dt),
                _rootPosY.Filter(pos.y, dt),
                _rootPosZ.Filter(pos.z, dt));

            var rot = input.bodyRotation;
            output.bodyRotation = new Quaternion(
                _rootRotX.Filter(rot.x, dt),
                _rootRotY.Filter(rot.y, dt),
                _rootRotZ.Filter(rot.z, dt),
                _rootRotW.Filter(rot.w, dt)).normalized;

            return output;
        }

        // 파라미터 변경 시 필터 재초기화
        void OnValidate()
        {
            if (_muscleFilters == null) return;
            for (int i = 0; i < _muscleFilters.Length; i++)
            {
                _muscleFilters[i].minCutoff = muscleMinCutoff;
                _muscleFilters[i].beta      = muscleBeta;
            }
        }

#if UNITY_EDITOR
        void OnDrawGizmosSelected()
        {
            if (!Application.isPlaying) return;
            string label = LastFrameInvalid
                ? $"<color=red>INVALID ({LastJitterCount} jitter)</color>"
                : $"<color=green>OK ({LastJitterCount} jitter)</color>";
            UnityEditor.Handles.Label(
                transform.position + Vector3.up * 2f, label);
        }
#endif
    }
}