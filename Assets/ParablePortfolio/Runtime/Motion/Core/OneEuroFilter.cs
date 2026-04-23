using System;
using UnityEngine;

namespace Parable.Motion
{
    /// <summary>
    /// One-Euro Filter — 실시간 모션 데이터 스무딩 알고리즘.
    ///
    /// 원리:
    ///   fc = fmin + beta * |velocity|
    ///   alpha = 1 - exp(-2π * fc * dt)
    ///   output = lerp(prev, current, alpha)
    ///
    ///   속도(velocity)가 크면 fc↑ → alpha↑ → 현재값에 빠르게 추종 (lag 방지)
    ///   속도(velocity)가 작으면 fc↓ → alpha↓ → 강하게 스무딩 (jitter 제거)
    ///
    /// 파라미터:
    ///   minCutoff(fmin): 정지 시 필터 강도. 낮을수록 부드러움 (권장: 1.0)
    ///   beta:            속도 반응 계수. 높을수록 빠른 동작에 덜 필터링 (권장: 0.0~0.5)
    ///   dCutoff:         velocity 필터의 컷오프. 일반적으로 1.0 고정
    ///
    /// 참고: Casiez et al. 2012, "1€ Filter"
    /// </summary>
    [Serializable]
    public struct OneEuroFilter
    {
        public float minCutoff;
        public float beta;
        public float dCutoff;

        float _prevFiltered;
        float _prevDerivative;
        bool  _initialized;

        public OneEuroFilter(float minCutoff = 1f, float beta = 0.1f, float dCutoff = 1f)
        {
            this.minCutoff = minCutoff;
            this.beta      = beta;
            this.dCutoff   = dCutoff;
            _prevFiltered   = 0f;
            _prevDerivative = 0f;
            _initialized    = false;
        }

        public float Filter(float value, float dt)
        {
            if (dt <= 0f) return value;

            if (!_initialized)
            {
                _prevFiltered   = value;
                _prevDerivative = 0f;
                _initialized    = true;
                return value;
            }

            // velocity 필터 (derivative 스무딩)
            float rawDeriv    = (value - _prevFiltered) / dt;
            float alphaD      = Alpha(dCutoff, dt);
            float derivative  = Mathf.Lerp(_prevDerivative, rawDeriv, alphaD);

            // 적응형 컷오프: 속도가 클수록 fc 높아짐
            float cutoff      = minCutoff + beta * Mathf.Abs(derivative);
            float alpha       = Alpha(cutoff, dt);
            float filtered    = Mathf.Lerp(_prevFiltered, value, alpha);

            _prevFiltered   = filtered;
            _prevDerivative = derivative;
            return filtered;
        }

        public void Reset()
        {
            _initialized = false;
        }

        static float Alpha(float cutoff, float dt)
        {
            float tau = 1f / (2f * Mathf.PI * cutoff);
            return 1f / (1f + tau / dt);
        }
    }
}