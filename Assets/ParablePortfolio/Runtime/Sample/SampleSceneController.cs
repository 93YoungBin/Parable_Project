using UnityEngine;
using UnityEngine.UI;

namespace Parable.Sample
{
    /// <summary>
    /// Sample 씬 최상위 오케스트레이터.
    ///
    /// 책임:
    ///   - AvatarClickSelector 이벤트 수신 → OutlineController, ShaderController에 전달
    ///   - UI 버튼을 각 Controller의 Toggle 메서드에 연결
    ///   - 선택된 아바타 이름 라벨 갱신
    ///
    /// 확장 포인트:
    ///   - 버튼에 아이콘/색상 피드백 추가
    ///   - 아바타별 상태(아웃라인 ON 여부 등) 표시 패널
    /// </summary>
    public class SampleSceneController : MonoBehaviour
    {
        [Header("Core")]
        public AvatarClickSelector  selector;
        public ToonOutlineController outlineController;
        public ToonShaderController  shaderController;

        [Header("UI — 선택 정보")]
        public TMPro.TMP_Text lblSelectedAvatar;

        [Header("UI — 아웃라인")]
        public Button btnOutlineToggle;

        [Header("UI — 툰 셰이더")]
        public Button btnShaderToggle;

        void Start()
        {
            selector.OnSelected += OnAvatarSelected;

            if (btnOutlineToggle) btnOutlineToggle.onClick.AddListener(outlineController.ToggleCurrent);
            if (btnShaderToggle)  btnShaderToggle.onClick.AddListener(shaderController.ToggleCurrent);

            RefreshLabel(null);
        }

        void OnDestroy()
        {
            if (selector) selector.OnSelected -= OnAvatarSelected;
        }

        void OnAvatarSelected(AvatarToonTarget target)
        {
            outlineController.OnAvatarSelected(target);
            shaderController.OnAvatarSelected(target);
            RefreshLabel(target);
            if(target != null)
            {
                target.SetAnimation();
            }
            
        }

        void RefreshLabel(AvatarToonTarget target)
        {
            if (lblSelectedAvatar == null) return;
            lblSelectedAvatar.text = target != null ? target.gameObject.name : "Select : Null";
        }
    }
}
