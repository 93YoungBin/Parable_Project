using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Parable.Motion
{
    /// <summary>
    /// 모션 파이프라인 입력값(클린업 이전)을 파일로 녹화.
    ///
    /// 목적:
    ///   MediaPipe 등 실시간 소스의 '날 것' 노이즈 데이터를 보존해
    ///   나중에 MotionPlayer로 재생 → 코드 노이즈 주입 없이
    ///   Stage 1 Cleanup 효과를 비교 시연.
    ///
    /// 녹화 시점:
    ///   MediaPipeLandmarkConverter.Apply() 이후 / cleanup.Process() 이전
    ///   → 실제 MediaPipe 노이즈가 담긴 raw HumanPose 캡처
    ///
    /// 파일 포맷: .pmocap (바이너리)
    ///   헤더 : magic(8B) + version(4B) + fps(4B) + frameCount(4B) + muscleCount(4B)
    ///   프레임: muscles(4B×n) + bodyPosition(12B) + bodyRotation(16B)
    ///
    /// 조작:
    ///   [R] 녹화 시작 / 중지+저장
    /// </summary>
    public class MotionRecorder : MonoBehaviour
    {
        [Header("저장 설정")]
        [Tooltip("Recordings/ 폴더에 저장될 파일명 접두사")]
        public string filePrefix = "mocap_raw";

        [Tooltip("저장 폴더 (프로젝트 루트 기준 상대경로)")]
        public string saveFolder = "Recordings";

        [Header("조작 키")]
        public KeyCode toggleKey = KeyCode.R;

        // ── 공개 상태 ─────────────────────────────────────────────
        public bool   IsRecording  { get; private set; }
        public int    FrameCount   { get; private set; }
        public string LastSavedPath { get; private set; }

        // ── 버퍼 ──────────────────────────────────────────────────
        readonly List<float[]>    _muscles   = new List<float[]>();
        readonly List<Vector3>    _positions = new List<Vector3>();
        readonly List<Quaternion> _rotations = new List<Quaternion>();

        const float RECORD_FPS = 30f;

        // ── 조작 ──────────────────────────────────────────────────

        void Update()
        {
            if (Input.GetKeyDown(toggleKey))
            {
                if (!IsRecording) StartRecording();
                else              StopAndSave();
            }
        }

        void StartRecording()
        {
            _muscles.Clear();
            _positions.Clear();
            _rotations.Clear();
            FrameCount  = 0;
            IsRecording = true;
            Debug.Log($"[MotionRecorder] 녹화 시작 ── [{toggleKey}] 로 중지·저장");
        }

        // ── MotionPipeline 에서 호출 ──────────────────────────────

        /// <summary>
        /// Cleanup 이전 raw HumanPose를 한 프레임 캡처.
        /// MotionPipeline.LateUpdate() 에서 내부 호출됨.
        /// </summary>
        public void CaptureFrame(ref HumanPose pose)
        {
            if (!IsRecording || pose.muscles == null) return;

            _muscles.Add((float[])pose.muscles.Clone());
            _positions.Add(pose.bodyPosition);
            _rotations.Add(pose.bodyRotation);
            FrameCount++;
        }

        // ── 저장 ──────────────────────────────────────────────────

        void StopAndSave()
        {
            IsRecording = false;

            if (FrameCount == 0)
            {
                Debug.LogWarning("[MotionRecorder] 녹화된 프레임 없음. 저장 취소.");
                return;
            }

            try
            {
                SaveBinary();
            }
            catch (Exception e)
            {
                Debug.LogError($"[MotionRecorder] 저장 실패: {e.Message}");
            }
        }

        void SaveBinary()
        {
            // 저장 경로: <프로젝트루트>/Recordings/
            string dir  = Path.GetFullPath(Path.Combine(Application.dataPath, "..", saveFolder));
            Directory.CreateDirectory(dir);

            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string path      = Path.Combine(dir, $"{filePrefix}_{timestamp}.pmocap");

            using var w = new BinaryWriter(File.Open(path, FileMode.Create));

            int muscleCount = _muscles[0].Length;

            // 헤더
            w.Write("PMOCAP\0\0".ToCharArray()); // magic 8B
            w.Write(1);                           // version
            w.Write(RECORD_FPS);
            w.Write(FrameCount);
            w.Write(muscleCount);

            // 프레임
            for (int i = 0; i < FrameCount; i++)
            {
                var m = _muscles[i];
                for (int j = 0; j < muscleCount; j++)
                    w.Write(m[j]);

                var p = _positions[i];
                w.Write(p.x); w.Write(p.y); w.Write(p.z);

                var r = _rotations[i];
                w.Write(r.x); w.Write(r.y); w.Write(r.z); w.Write(r.w);
            }

            LastSavedPath = path;
            Debug.Log($"[MotionRecorder] 저장 완료 ({FrameCount} 프레임)\n{path}");
        }

        // ── GUI ───────────────────────────────────────────────────

        void OnGUI()
        {
            if (!IsRecording) return;

            var style = new GUIStyle(GUI.skin.box) { fontSize = 15, alignment = TextAnchor.MiddleCenter };
            style.normal.textColor = Color.red;
            GUI.Box(new Rect(Screen.width / 2 - 100, 10, 200, 36),
                    $"● REC  {FrameCount} frames", style);
        }
    }
}
