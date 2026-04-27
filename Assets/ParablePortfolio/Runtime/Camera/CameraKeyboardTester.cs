using UnityEngine;
using UnityEngine.UI;

namespace Parable.Camera
{
    /// <summary>
    /// 카메라 시스템 데모 테스터.
    ///
    /// 키 조작:
    ///   1 / 2 / 3 / 4 : 슬롯 직접 전환
    ///   Space          : 자동 시퀀스 시작/정지 (AnimationEvent 타이밍 시뮬레이션)
    /// </summary>
    public class CameraKeyboardTester : MonoBehaviour
    {
        public CameraDirector director;

        [Header("Auto Sequence")]
        [Tooltip("자동 시퀀스 각 슬롯 체류 시간 (초)")]
        public float[] sequenceDurations = { 3f, 4f, 5f, 4f };

        bool  _autoRunning;
        int   _seqIndex;
        float _seqTimer;
        bool  _cutMode;

        void Start()
        {
            WireButtons();

            if (director != null)
                director.SwitchTo(0);
        }

        void WireButtons()
        {
            // Canvas 아래 Button_0~3, CutButton을 이름으로 찾아 자동 연결.
            // Inspector 직렬화 없이 Play마다 재연결 → 씬 저장 여부 무관하게 항상 유지.
            var canvas = FindObjectOfType<Canvas>();
            if (canvas == null) return;

            string[] camBtnNames = { "Button_0", "Button_1", "Button_2", "Button_3" };
            for (int i = 0; i < camBtnNames.Length; i++)
            {
                var t = canvas.transform.Find(camBtnNames[i]);
                if (t == null) continue;
                var btn = t.GetComponent<Button>();
                if (btn == null) continue;

                btn.onClick.RemoveAllListeners();
                int idx = i;   // 클로저 캡처용 복사
                btn.onClick.AddListener(() => SelectCamera(idx));
            }

            var cutT = canvas.transform.Find("CutButton");
            if (cutT != null)
            {
                var cutBtn = cutT.GetComponent<Button>();
                if (cutBtn != null)
                {
                    cutBtn.onClick.RemoveAllListeners();
                    cutBtn.onClick.AddListener(ToggleCutMode);
                }
            }
        }

        void Update()
        {
            if (director == null) return;

            // 0 : Cut Mode 토글
            if (Input.GetKeyDown(KeyCode.Alpha0)) _cutMode = !_cutMode;

            if (Input.GetKeyDown(KeyCode.Alpha1)) { StopAuto(); director.SwitchTo(0, _cutMode); }
            if (Input.GetKeyDown(KeyCode.Alpha2)) { StopAuto(); director.SwitchTo(1, _cutMode); }
            if (Input.GetKeyDown(KeyCode.Alpha3)) { StopAuto(); director.SwitchTo(2, _cutMode); }
            if (Input.GetKeyDown(KeyCode.Alpha4)) { StopAuto(); director.SwitchTo(3, _cutMode); }

            if (Input.GetKeyDown(KeyCode.Space))
            {
                if (_autoRunning) StopAuto();
                else              StartAuto();
            }

            if (_autoRunning)
            {
                _seqTimer -= Time.deltaTime;
                if (_seqTimer <= 0f)
                {
                    _seqIndex = (_seqIndex + 1) % director.cameras.Count;
                    director.SwitchTo(_seqIndex, _cutMode);
                    _seqTimer = GetDuration(_seqIndex);
                }
            }
        }

        void StartAuto()
        {
            _autoRunning = true;
            _seqIndex    = director.ActiveIndex;
            _seqTimer    = GetDuration(_seqIndex);
        }

        void StopAuto() => _autoRunning = false;

        // ── UI 버튼용 public 메서드 ───────────────────────────────
        public void SelectCamera(int index) { StopAuto(); director?.SwitchTo(index, _cutMode); }
        public void ToggleCutMode() { _cutMode = !_cutMode; }

        float GetDuration(int idx)
        {
            if (sequenceDurations == null || sequenceDurations.Length == 0) return 3f;
            return sequenceDurations[idx % sequenceDurations.Length];
        }
    }
}
