using UnityEngine;

namespace Parable.Motion
{
    // ────────────────────────────────────────────────────────────────────────────
    // 필터 알고리즘 선택
    // ────────────────────────────────────────────────────────────────────────────
    public enum CleanupFilterMode
    {
        /// <summary>지수이동평균 — 구현 단순, 고정 지연</summary>
        EMA,

        /// <summary>스칼라 칼만 필터 — 불확실성 기반 가변 게인, 빠른 수렴</summary>
        Kalman,
    }

    /// <summary>
    /// Stage 1 — 실시간 모캡 클린업.
    ///
    /// ┌──────────────────────────────────────────────────────────────┐
    /// │  문제: MediaPipe 웹캠 모캡은 두 가지 노이즈 발생              │
    /// │   ① 고주파 지터   — 매 프레임 ±소폭 흔들림 (센서/조명 잡음) │
    /// │   ② 아웃라이어    — 마커 가림 시 1-N프레임 값 급등           │
    /// └──────────────────────────────────────────────────────────────┘
    ///
    /// 해결:
    ///   ① 저역 통과 필터 (EMA 또는 칼만) — 고주파 지터 감쇠
    ///   ② 아웃라이어 Rejection           — last-good 값으로 복원
    ///
    /// 필터 선택:
    ///   EMA    — 구현 단순, 파라미터 직관적. alpha = 0.25 권장.
    ///   Kalman — 불확실성(P)이 높을 때 게인↑(빠른 수렴),
    ///            낮을 때 게인↓(강한 필터). 마커 재등장 후 복원 빠름.
    ///
    /// 핵심 트레이드오프 (패러블 아티클 참고):
    ///   필터 강도 ↑ → 지터 감소 / 퍼포머 표현 에너지 손실
    ///   필터 강도 ↓ → 표현 유지 / 지터 잔존
    /// </summary>
    [System.Serializable]
    public class MotionCleanupModule
    {
        [Tooltip("false면 이 단계를 건너뜀 (파이프라인 단계별 독립 테스트용)")]
        public bool enabled = true;

        // ── 필터 모드 ────────────────────────────────────────────────
        [Tooltip("EMA: 단순 지수이동평균\nKalman: 불확실성 기반 가변 게인 (마커 가림 후 복원 빠름)")]
        public CleanupFilterMode filterMode = CleanupFilterMode.Kalman;

        // ── EMA 파라미터 ─────────────────────────────────────────────
        [Header("EMA 파라미터 (filterMode = EMA 일 때)")]
        [Tooltip("EMA 스무딩 계수. 1=즉각 반응(필터 없음), 0.01=강한 스무딩\n" +
                 "낮출수록 지터는 줄지만 퍼포머 표현 에너지도 줄어듦 (핵심 트레이드오프)\n\n" +
                 "권장값:\n" +
                 "  MediaPipe 웹캠 → 0.15 ~ 0.25\n" +
                 "  고품질 광학식   → 0.3 ~ 0.5")]
        [Range(0.01f, 1f)] public float smoothingAlpha = 0.25f;

        // ── 칼만 필터 파라미터 ───────────────────────────────────────
        [Header("칼만 필터 파라미터 (filterMode = Kalman 일 때)")]
        [Tooltip("프로세스 노이즈 분산 Q. '시스템이 프레임마다 얼마나 변할 수 있는가'\n" +
                 "높을수록 필터가 측정값을 더 신뢰 (빠른 추적, 지터 잔존)\n\n" +
                 "권장값:\n" +
                 "  MediaPipe 웹캠 → 0.0005 ~ 0.002\n" +
                 "  고품질 광학식   → 0.001 ~ 0.005")]
        [Range(0.00001f, 0.1f)] public float kalmanQ = 0.001f;

        [Tooltip("측정 노이즈 분산 R. '센서(MediaPipe)가 얼마나 불확실한가'\n" +
                 "높을수록 측정값을 불신 → 강한 필터\n\n" +
                 "권장값:\n" +
                 "  MediaPipe 웹캠 → 0.05 ~ 0.2\n" +
                 "  고품질 광학식   → 0.01 ~ 0.05")]
        [Range(0.001f, 1f)] public float kalmanR = 0.08f;

        // ── 아웃라이어 제거 ──────────────────────────────────────────
        [Header("아웃라이어 제거 (공통)")]
        [Tooltip("한 프레임 내 muscle 변화량이 이 값 초과 시 last-good 값으로 복원.\n" +
                 "0 = 비활성화 (권장: MediaPipe 빠른 동작 오탐 방지)\n\n" +
                 "권장값:\n" +
                 "  MediaPipe 웹캠 → 0 (비활성)\n" +
                 "  광학식 모캡    → 0.3 ~ 0.5")]
        [Range(0f, 2f)] public float outlierThreshold = 0f;

        // ── EMA 내부 상태 ─────────────────────────────────────────────
        float[] _emaState;
        bool    _emaInitialized;

        // ── 칼만 내부 상태 ────────────────────────────────────────────
        float[] _kalmanX; // 추정 상태 (filtered muscle value)
        float[] _kalmanP; // 오차 공분산 (error covariance)
        bool    _kalmanInitialized;

        // ── 공개 API ─────────────────────────────────────────────────

        /// <summary>
        /// HumanPose를 직접 수정 (in-place). LateUpdate에서 호출.
        /// </summary>
        public void Process(ref HumanPose pose)
        {
            if (!enabled) return;

            int n = pose.muscles.Length;

            switch (filterMode)
            {
                case CleanupFilterMode.EMA:    ProcessEMA   (ref pose, n); break;
                case CleanupFilterMode.Kalman: ProcessKalman(ref pose, n); break;
            }
        }

        public void Reset()
        {
            _emaInitialized    = false;
            _kalmanInitialized = false;
        }

        // ── EMA ──────────────────────────────────────────────────────

        /// <summary>
        /// 지수이동평균 필터.
        ///
        /// 수식: x_t = α·z_t + (1−α)·x_(t−1)
        ///   α = smoothingAlpha
        ///   z = 측정값 (raw muscle)
        ///   x = 추정값 (filtered muscle)
        ///
        /// 지연량 (τ 프레임): τ ≈ 1/α − 1
        ///   α=0.25 → ~3 프레임 지연
        ///   α=0.1  → ~9 프레임 지연
        /// </summary>
        void ProcessEMA(ref HumanPose pose, int n)
        {
            if (!_emaInitialized || _emaState == null || _emaState.Length != n)
            {
                _emaState       = (float[])pose.muscles.Clone();
                _emaInitialized = true;
                return; // 첫 프레임은 그대로 통과
            }

            for (int i = 0; i < n; i++)
            {
                float z = pose.muscles[i]; // 측정값

                // 아웃라이어 Rejection
                if (outlierThreshold > 0f &&
                    Mathf.Abs(z - _emaState[i]) > outlierThreshold)
                {
                    z = _emaState[i]; // last-good 값 유지
                }

                // EMA 업데이트 (= Mathf.Lerp)
                _emaState[i]    = Mathf.Lerp(_emaState[i], z, smoothingAlpha);
                pose.muscles[i] = _emaState[i];
            }
        }

        // ── 칼만 필터 ────────────────────────────────────────────────

        /// <summary>
        /// 스칼라 칼만 필터 (1D per muscle).
        ///
        /// 모델: 상수 모델 (등속도 없음) — x_k = x_(k-1) + w,  w ~ N(0, Q)
        ///
        /// 예측 단계:
        ///   x̂_k|k-1  = x̂_(k-1)         (이전 추정값 그대로)
        ///   P_k|k-1  = P_(k-1) + Q       (불확실성 증가)
        ///
        /// 업데이트 단계:
        ///   K        = P_k|k-1 / (P_k|k-1 + R)    (칼만 게인, 0~1)
        ///   x̂_k      = x̂_k|k-1 + K·(z_k − x̂_k|k-1)  (측정으로 보정)
        ///   P_k      = (1 − K)·P_k|k-1              (불확실성 갱신)
        ///
        /// EMA와의 차이:
        ///   EMA: 게인 α 고정 → 항상 동일 지연
        ///   Kalman: 게인 K 가변 → 마커 재등장 직후 K↑(빠른 수렴),
        ///           안정 구간 K↓(강한 필터)
        /// </summary>
        void ProcessKalman(ref HumanPose pose, int n)
        {
            if (!_kalmanInitialized || _kalmanX == null || _kalmanX.Length != n)
            {
                _kalmanX = (float[])pose.muscles.Clone();
                _kalmanP = new float[n];
                for (int i = 0; i < n; i++) _kalmanP[i] = 1f; // 초기 불확실성 높게
                _kalmanInitialized = true;
                return;
            }

            for (int i = 0; i < n; i++)
            {
                float z = pose.muscles[i]; // 측정값

                // 아웃라이어 Rejection
                if (outlierThreshold > 0f &&
                    Mathf.Abs(z - _kalmanX[i]) > outlierThreshold)
                {
                    z = _kalmanX[i];
                }

                // 예측 단계 (Predict)
                float xPred = _kalmanX[i];           // 상태 예측 (상수 모델)
                float pPred = _kalmanP[i] + kalmanQ;  // 공분산 예측 (불확실성 증가)

                // 업데이트 단계 (Update)
                float K      = pPred / (pPred + kalmanR);   // 칼만 게인
                _kalmanX[i]  = xPred + K * (z - xPred);     // 상태 업데이트
                _kalmanP[i]  = (1f - K) * pPred;             // 공분산 업데이트

                pose.muscles[i] = _kalmanX[i];
            }
        }
    }
}
