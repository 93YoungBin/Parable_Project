using UnityEngine;
using UnityEngine.Rendering;

namespace Parable.Sample
{
    /// <summary>
    /// 아바타 루트에 붙이는 컴포넌트.
    /// - 자식 Renderer[] 자동 수집
    /// - 레이캐스트용 CapsuleCollider 자동 생성 (없을 경우)
    /// - 모든 자식 Renderer의 Cast Shadows 강제 활성화 (VRM 기본값이 Off인 경우 대응)
    /// </summary>
    public class AvatarToonTarget : MonoBehaviour
    {
        [Tooltip("비워두면 Awake에서 GetComponentsInChildren으로 자동 수집")]
        public Renderer[] renderers;

        [Tooltip("Awake 시 모든 자식 Renderer의 Cast Shadows를 강제로 On으로 설정")]
        public bool forceCastShadows = true;

        [Header("Auto Collider")]
        public float colliderHeight = 1.7f;
        public float colliderRadius = 0.3f;
        public Vector3 colliderCenter = new Vector3(0f, 0.85f, 0f);

        public bool IsSelected { get; private set; }

        private Animator animator;

        void Awake()
        {
            if (renderers == null || renderers.Length == 0)
                renderers = GetComponentsInChildren<Renderer>(includeInactive: false);

            // 클릭 감지용 콜라이더가 없으면 자동 추가
            if (GetComponent<Collider>() == null)
            {
                var col      = gameObject.AddComponent<CapsuleCollider>();
                col.height   = colliderHeight;
                col.radius   = colliderRadius;
                col.center   = colliderCenter;
            }

            if(animator == null)
            {
                animator = gameObject.GetComponentInChildren<Animator>();
            }

            // VRM은 Export 설정에 따라 Cast Shadows = Off 로 나오는 경우가 있음
            if (forceCastShadows)
            {
                foreach (var r in renderers)
                    if (r != null) r.shadowCastingMode = ShadowCastingMode.On;
            }
        }

        public void SetSelected(bool selected) => IsSelected = selected;

        public void SetAnimation()
        {
            animator.SetTrigger("IsSelect");
        }
    }
}
