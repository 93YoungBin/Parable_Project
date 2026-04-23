using System;
using UnityEngine;
using UnityEngine.EventSystems;
using Camera = UnityEngine.Camera;

namespace Parable.Sample
{
    /// <summary>
    /// 마우스 클릭 레이캐스트로 AvatarToonTarget을 선택.
    /// OnSelected 이벤트로 선택된 타겟을 외부에 알림.
    /// </summary>
    public class AvatarClickSelector : MonoBehaviour
    {
        [Header("Raycast")]
        public UnityEngine.Camera raycastCamera;
        public LayerMask raycastMask = ~0;
        public float     raycastDistance = 100f;

        /// <summary>아바타 선택 시 발생. null = 선택 해제</summary>
        public event Action<AvatarToonTarget> OnSelected;

        AvatarToonTarget _current;

        void Awake()
        {
            if (raycastCamera == null)
                raycastCamera = UnityEngine.Camera.main;
        }

        void Update()
        {
            if (!Input.GetMouseButtonDown(0)) return;

            // UI 버튼 위 클릭은 레이캐스트 무시 (버튼 클릭이 선택 해제되는 문제 방지)
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
                return;

            var ray = raycastCamera.ScreenPointToRay(Input.mousePosition);
            if (!Physics.Raycast(ray, out var hit, raycastDistance, raycastMask))
            {
                // 빈 공간 클릭 → 선택 해제
                Select(null);
                return;
            }

            // 히트한 GO의 계층에서 AvatarToonTarget 탐색
            var target = hit.collider.GetComponentInParent<AvatarToonTarget>();
            Select(target);
        }

        void Select(AvatarToonTarget next)
        {
            if (_current == next) return;

            _current?.SetSelected(false);
            _current = next;
            _current?.SetSelected(true);

            OnSelected?.Invoke(_current);
        }

        public AvatarToonTarget Current => _current;
    }
}
