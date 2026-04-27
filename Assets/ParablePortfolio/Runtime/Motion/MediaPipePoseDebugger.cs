using UnityEngine;

namespace Parable.Motion
{
    /// <summary>
    /// MediaPipePoseTracker의 world landmark를 Scene/Game 뷰에 시각화.
    ///
    /// 사용법:
    ///   MediaPipePoseTracker와 같은 GameObject에 추가.
    ///   Game 뷰에서 보려면 Game 뷰 상단 Gizmos 버튼 활성화.
    ///   Scene 뷰는 항상 표시.
    /// </summary>
    [RequireComponent(typeof(MediaPipePoseTracker))]
    public class MediaPipePoseDebugger : MonoBehaviour
    {
        [Header("시각화 설정")]
        [Tooltip("랜드마크 구체 크기")]
        public float sphereRadius = 0.03f;

        [Tooltip("스켈레톤 표시")]
        public bool showSkeleton = true;

        [Tooltip("랜드마크 번호 표시 (Scene 뷰만)")]
        public bool showIndices = false;

        [Tooltip("표시 위치 오프셋 (아바타와 겹치지 않게)")]
        public Vector3 displayOffset = new Vector3(1.5f, 1f, 0f);

        [Tooltip("가시성 임계값 이하 랜드마크는 반투명 표시")]
        [Range(0f, 1f)] public float visThreshold = 0.5f;

        // 색상
        public Color colorFace      = Color.yellow;
        public Color colorTorso     = Color.white;
        public Color colorLeftArm   = Color.green;
        public Color colorRightArm  = Color.cyan;
        public Color colorLeftLeg   = new Color(0.2f, 1f, 0.2f);
        public Color colorRightLeg  = new Color(0.2f, 0.8f, 1f);
        public Color colorLow       = new Color(0.5f, 0.5f, 0.5f, 0.3f);

        // ── MediaPipe Pose 스켈레톤 연결 정의 ───────────────────────
        // (from, to, color)
        static readonly (int, int, int)[] CONNECTIONS = {
            // 얼굴
            (0, 1, 0), (1, 2, 0), (2, 3, 0), (3, 7, 0),
            (0, 4, 0), (4, 5, 0), (5, 6, 0), (6, 8, 0),
            (9, 10, 0),
            // 어깨
            (11, 12, 1),
            // 좌측 팔
            (11, 13, 2), (13, 15, 2),
            // 우측 팔
            (12, 14, 3), (14, 16, 3),
            // 몸통
            (11, 23, 1), (12, 24, 1), (23, 24, 1),
            // 좌측 다리
            (23, 25, 4), (25, 27, 4), (27, 29, 4), (29, 31, 4),
            // 우측 다리
            (24, 26, 5), (26, 28, 5), (28, 30, 5), (30, 32, 5),
        };

        MediaPipePoseTracker _tracker;
        readonly Vector3[] _landmarks    = new Vector3[MediaPipePoseTracker.LANDMARK_COUNT];
        readonly float[]   _visibilities = new float[MediaPipePoseTracker.LANDMARK_COUNT];
        bool _hasPose;

        void Awake()
        {
            _tracker = GetComponent<MediaPipePoseTracker>();
        }

        void Update()
        {
            if (_tracker == null || !_tracker.IsRunning) return;

            // 매 프레임 최신 랜드마크 읽기
            if (_tracker.TryGetWorldLandmarks(_landmarks, _visibilities))
                _hasPose = true;

            if (!_hasPose) return;

            // Debug.DrawLine → Scene + Game 뷰 모두 표시 (duration=0 = 이번 프레임만)
            if (showSkeleton)
                DrawSkeleton();
        }

        void DrawSkeleton()
        {
            foreach (var (from, to, colorIdx) in CONNECTIONS)
            {
                if (from >= _landmarks.Length || to >= _landmarks.Length) continue;

                var a = _landmarks[from] + displayOffset;
                var b = _landmarks[to]   + displayOffset;

                float vis = Mathf.Min(_visibilities[from], _visibilities[to]);
                Color c = vis >= visThreshold ? GetColor(colorIdx) : colorLow;

                Debug.DrawLine(a, b, c);
            }

            // 랜드마크 점 (짧은 십자선)
            for (int i = 0; i < _landmarks.Length; i++)
            {
                var p  = _landmarks[i] + displayOffset;
                float vis = _visibilities[i];
                Color c = vis >= visThreshold ? Color.white : colorLow;
                float r = sphereRadius;

                Debug.DrawLine(p - Vector3.right * r,   p + Vector3.right * r,   c);
                Debug.DrawLine(p - Vector3.up    * r,   p + Vector3.up    * r,   c);
                Debug.DrawLine(p - Vector3.forward * r, p + Vector3.forward * r, c);
            }
        }

        Color GetColor(int idx) => idx switch
        {
            0 => colorFace,
            1 => colorTorso,
            2 => colorLeftArm,
            3 => colorRightArm,
            4 => colorLeftLeg,
            5 => colorRightLeg,
            _ => Color.white,
        };

        // ── Gizmos (Scene 뷰 + Game 뷰 Gizmos ON) ──────────────────
        void OnDrawGizmos()
        {
            if (!Application.isPlaying || !_hasPose) return;

            // 랜드마크 구체
            for (int i = 0; i < _landmarks.Length; i++)
            {
                float vis = _visibilities[i];
                Gizmos.color = vis >= visThreshold
                    ? new Color(1f, 1f, 0f, vis)
                    : new Color(0.5f, 0.5f, 0.5f, 0.2f);

                Gizmos.DrawSphere(_landmarks[i] + displayOffset, sphereRadius);

#if UNITY_EDITOR
                if (showIndices)
                {
                    UnityEditor.Handles.Label(
                        _landmarks[i] + displayOffset + Vector3.up * (sphereRadius * 2),
                        i.ToString());
                }
#endif
            }

            // 스켈레톤 선
            if (showSkeleton)
            {
                foreach (var (from, to, colorIdx) in CONNECTIONS)
                {
                    if (from >= _landmarks.Length || to >= _landmarks.Length) continue;
                    float vis = Mathf.Min(_visibilities[from], _visibilities[to]);
                    Gizmos.color = vis >= visThreshold ? GetColor(colorIdx) : colorLow;
                    Gizmos.DrawLine(
                        _landmarks[from] + displayOffset,
                        _landmarks[to]   + displayOffset);
                }
            }
        }

        // ── GUI 오버레이 (Game 뷰 상태 표시) ────────────────────────
        void OnGUI()
        {
            if (_tracker == null) return;

            var style = new GUIStyle(GUI.skin.box)
            {
                fontSize  = 13,
                alignment = TextAnchor.UpperLeft,
            };
            style.normal.textColor = Color.white;

            string status = _tracker.IsRunning
                ? (_hasPose ? "<color=#00ff88>● POSE DETECTED</color>"
                            : "<color=#ffaa00>● WAITING FOR POSE...</color>")
                : "<color=#ff4444>● NOT RUNNING</color>";

            string text = $"MediaPipe Pose Tracker\n" +
                          $"Status : {(_tracker.IsRunning ? "Running" : "Stopped")}\n" +
                          $"HasPose: {_tracker.HasPose}\n" +
                          $"Frames : {_tracker.FrameCount}\n";

            GUI.Box(new Rect(10, 10, 220, 80), text, style);
        }
    }
}
