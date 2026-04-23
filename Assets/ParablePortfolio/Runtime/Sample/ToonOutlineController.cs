using System.Collections.Generic;
using UnityEngine;

namespace Parable.Sample
{
    public class ToonOutlineController : MonoBehaviour
    {
        [Tooltip("ToonRendererFeature.settings.outlineRenderingLayerMask 와 동일한 비트 (기본 256 = 1<<8)")]
        public uint outlineBit = 1u << 8;

        // 아바타별 아웃라인 ON/OFF 상태 (renderingLayerMask 기본값이 all-set이라 직접 추적)
        readonly HashSet<int> _outlineOn = new HashSet<int>();

        AvatarToonTarget _current;

        void Start()
        {
            // renderingLayerMask 기본값 = uint.MaxValue (모든 비트 ON).
            // 씬 내 모든 Renderer에서 아웃라인 비트를 제거해 초기 상태를 OFF로 통일.
            foreach (var r in FindObjectsOfType<Renderer>())
                r.renderingLayerMask &= ~outlineBit;
        }

        public void OnAvatarSelected(AvatarToonTarget target)
        {
            _current = target;
        }

        /// <summary>UI 버튼 → 현재 선택된 아바타 아웃라인 토글</summary>
        public void ToggleCurrent()
        {
            if (_current == null) return;

            int id = _current.GetInstanceID();
            bool isOn = _outlineOn.Contains(id);

            SetOutline(_current, !isOn);
        }

        void SetOutline(AvatarToonTarget target, bool on)
        {
            if (target == null) return;
            int id = target.GetInstanceID();

            if (on) _outlineOn.Add(id);
            else    _outlineOn.Remove(id);

            foreach (var r in target.renderers)
            {
                if (r == null) continue;
                if (on) r.renderingLayerMask |=  outlineBit;
                else    r.renderingLayerMask &= ~outlineBit;
            }
        }
    }
}
