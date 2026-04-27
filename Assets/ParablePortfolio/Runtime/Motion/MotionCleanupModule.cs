using UnityEngine;

namespace Parable.Motion
{
    /// <summary>
    /// Stage 1 — 실시간 모캡 클린업.
    ///
    /// Parable 아티클:
    ///   "Real-time Cleanup Stage: 마커 가림 감지·보정.
    ///    과하면 에너지 손실, 부족하면 지터"
    ///
    /// 구현:
    ///   ① EMA (지수이동평균) — 고주파 지터 제거
    ///   ② Outlier Rejection — 한 프레임에 너무 크게 뛰는 값 = 마커 가림으로 판단,
    ///      이전 값으로 복원 (마커가 다시 보일 때까지 last-good 값 유지)
    ///
    /// 트레이드오프:
    ///   smoothingAlpha 높음 → 즉각 반응, 지터 잔존
    ///   smoothingAlpha 낮음 → 부드럽지만 표현 에너지 손실 (Parable 아티클 핵심 언급)
    ///   outlierThreshold 낮음 → 가림 감지 민감, 정상 큰 동작도 차단될 수 있음
    /// </summary>
    [System.Serializable]
    public class MotionCleanupModule
    {
        [Tooltip("false면 이 단계를 건너뜀 (파이프라인 단계별 독립 테스트)")]
        public bool enabled = true;

        [Tooltip("EMA 스무딩 계수. 1=즉각 반응(필터 없음), 0.1=부드러운 추적\n" +
                 "낮출수록 지터는 줄지만 퍼포머 표현 에너지도 줄어듦 (핵심 트레이드오프)")]
        [Range(0.01f, 1f)] public float smoothingAlpha = 0.3f;

        [Tooltip("아웃라이어(마커 가림) 감지 임계값.\n" +
                 "한 프레임 내 muscle 변화량이 이 값 초과 시 이전 값 복원.\n" +
                 "0 = 비활성화")]
        [Range(0f, 2f)] public float outlierThreshold = 0.5f;

        float[] _prev;
        bool    _initialized;

        /// <summary>
        /// HumanPose를 직접 수정 (in-place). LateUpdate에서 호출.
        /// </summary>
        public void Process(ref HumanPose pose)
        {
            int muscleCount = pose.muscles.Length;

            // 첫 프레임 초기화
            if (!_initialized || _prev == null || _prev.Length != muscleCount)
            {
                _prev        = (float[])pose.muscles.Clone();
                _initialized = true;
                return; // 첫 프레임은 그대로 통과
            }

            for (int i = 0; i < muscleCount; i++)
            {
                float current = pose.muscles[i];

                // ① Outlier Rejection: 급격한 값 점프 = 마커 가림으로 판단
                if (outlierThreshold > 0f &&
                    Mathf.Abs(current - _prev[i]) > outlierThreshold)
                {
                    current = _prev[i]; // last-good 값으로 복원
                }

                // ② EMA: 지수이동평균
                _prev[i]        = Mathf.Lerp(_prev[i], current, smoothingAlpha);
                pose.muscles[i] = _prev[i];
            }
        }

        public void Reset()
        {
            _initialized = false;
        }
    }
}
