using System;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Parable.Rendering
{
    /// <summary>
    /// Toon 렌더링용 Shadow 설정 컨테이너.
    /// ToonShadowControlFeature가 이 에셋을 읽어 URP 파이프라인에 런타임으로 주입.
    /// </summary>
    [CreateAssetMenu(fileName = "ToonShadowSettings",
                     menuName  = "Parable/Toon Shadow Settings")]
    public class ToonShadowSettings : ScriptableObject
    {
        public bool enabled = true;

        [Header("Shadow Distance")]
        [Min(0f)] public float shadowDistance = 60f;

        [Header("Cascade")]
        [Range(1, 4)] public int cascadeCount = 4;

        [Tooltip("Cascade 2 분할 지점 (0~1 비율)")]
        [Range(0.01f, 0.98f)] public float split1 = 0.067f;

        [Tooltip("Cascade 3 두 번째 분할 지점")]
        [Range(0.02f, 0.99f)] public float split2 = 0.200f;

        [Tooltip("Cascade 4 세 번째 분할 지점")]
        [Range(0.03f, 0.99f)] public float split3 = 0.467f;

        [Header("Shadow Bias")]
        [Range(0f, 3f)] public float depthBias  = 1.0f;
        [Range(0f, 3f)] public float normalBias = 1.0f;

        [Header("Soft Shadows")]
        public bool softShadows = true;
    }
}