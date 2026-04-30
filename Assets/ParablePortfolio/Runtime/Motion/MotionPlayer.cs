using System;
using System.IO;
using UnityEngine;

namespace Parable.Motion
{
    /// <summary>
    /// MotionRecorder가 저장한 .pmocap 파일을 재생.
    ///
    /// 목적:
    ///   녹화된 raw 노이즈 데이터를 반복 재생하면서
    ///   Stage 1 Cleanup ON/OFF 효과를 비교 시연.
    ///   → MediaPipe 없이도, 코드 노이즈 주입 없이도 시연 가능.
    ///
    /// 사용법:
    ///   1. MotionPipeline 과 같은 GameObject에 추가
    ///   2. File Name 에 Recordings/ 폴더의 .pmocap 파일명 입력
    ///   3. Play 모드 진입 → [Space] 재생/정지, [Backspace] 되감기
    ///   4. MotionPipeline Inspector → cleanup.enabled 토글로 비교
    ///
    /// 조작:
    ///   [Space]     재생 / 정지
    ///   [Backspace] 처음으로 되감기
    /// </summary>
    public class MotionPlayer : MonoBehaviour
    {
        [Header("파일")]
        [Tooltip("Recordings/ 폴더 기준 파일명 (예: mocap_raw_20260428_120000.pmocap)")]
        public string fileName = "";

        [Header("재생 설정")]
        public bool  loop        = true;
        public bool  playOnLoad  = true;
        [Range(0.1f, 3f)] public float speed = 1f;

        [Tooltip("true: 파일에 저장된 bodyPosition/Rotation도 복원 (실제 녹화 재생 시)\n" +
                 "false: bodyPosition/Rotation은 Animator에 맡김 (더미 데이터 사용 시 권장)")]
        public bool overrideBodyTransform = false;

        [Header("조작 키")]
        public KeyCode playPauseKey = KeyCode.Space;
        public KeyCode rewindKey    = KeyCode.Backspace;

        // ── 공개 상태 ─────────────────────────────────────────────
        public bool  IsLoaded     { get; private set; }
        public bool  IsPlaying    { get; private set; }
        public int   FrameCount   { get; private set; }
        public int   CurrentFrame { get; private set; }
        public float Progress     => FrameCount > 0 ? (float)CurrentFrame / FrameCount : 0f;
        public float DurationSec  => FrameCount > 0 ? FrameCount / _fps : 0f;

        // ── 내부 데이터 ───────────────────────────────────────────
        float[][]    _muscles;
        Vector3[]    _positions;
        Quaternion[] _rotations;
        int          _muscleCount;
        float        _fps = 30f;
        float        _timer;

        // ── 생명주기 ─────────────────────────────────────────────

        void Start()
        {
            if (!string.IsNullOrEmpty(fileName))
                Load(fileName);
        }

        void Update()
        {
            if (Input.GetKeyDown(playPauseKey)) TogglePlay();
            if (Input.GetKeyDown(rewindKey))    Rewind();

            if (IsPlaying) AdvanceTime();
        }

        // ── 로드 ──────────────────────────────────────────────────

        public void Load(string file)
        {
            string path = Path.GetFullPath(
                Path.Combine(Application.dataPath, "..", "Recordings", file));

            if (!File.Exists(path))
            {
                Debug.LogError($"[MotionPlayer] 파일 없음: {path}");
                return;
            }

            try
            {
                LoadBinary(path);
            }
            catch (Exception e)
            {
                Debug.LogError($"[MotionPlayer] 로드 실패: {e.Message}");
            }
        }

        void LoadBinary(string path)
        {
            using var r = new BinaryReader(File.Open(path, FileMode.Open));

            // 헤더 검증
            string magic = new string(r.ReadChars(8)).TrimEnd('\0');
            if (magic != "PMOCAP")
            {
                Debug.LogError("[MotionPlayer] 잘못된 파일 형식 (magic 불일치)");
                return;
            }

            int version  = r.ReadInt32();
            _fps         = r.ReadSingle();
            FrameCount   = r.ReadInt32();
            _muscleCount = r.ReadInt32();

            // 배열 할당
            _muscles   = new float[FrameCount][];
            _positions = new Vector3[FrameCount];
            _rotations = new Quaternion[FrameCount];

            for (int i = 0; i < FrameCount; i++)
            {
                _muscles[i] = new float[_muscleCount];
                for (int j = 0; j < _muscleCount; j++)
                    _muscles[i][j] = r.ReadSingle();

                float px = r.ReadSingle(), py = r.ReadSingle(), pz = r.ReadSingle();
                _positions[i] = new Vector3(px, py, pz);

                float rx = r.ReadSingle(), ry = r.ReadSingle(),
                      rz = r.ReadSingle(), rw = r.ReadSingle();
                _rotations[i] = new Quaternion(rx, ry, rz, rw);
            }

            IsLoaded     = true;
            CurrentFrame = 0;
            _timer       = 0f;

            float sec = FrameCount / _fps;
            Debug.Log($"[MotionPlayer] 로드 완료 ── {FrameCount} 프레임 / {sec:F1}초 / {_fps}fps\n{path}");

            if (playOnLoad) IsPlaying = true;
        }

        // ── 메인 스레드 API ──────────────────────────────────────

        /// <summary>
        /// 현재 프레임의 raw 포즈를 pose 에 덮어씀.
        /// MotionPipeline.LateUpdate() 에서 Cleanup 이전에 호출됨.
        /// muscles 배열 길이가 맞지 않으면 false 반환.
        /// </summary>
        public bool TryGetCurrentPose(ref HumanPose pose)
        {
            if (!IsLoaded || !IsPlaying) return false;
            if (pose.muscles == null)   return false;

            // muscle 수 불일치: bodyPosition/Rotation만 복원하고 muscles는 스킵
            int copyLen = Mathf.Min(pose.muscles.Length, _muscleCount);
            if (copyLen != _muscleCount)
            {
                Debug.LogWarning($"[MotionPlayer] muscle 수 불일치 " +
                                 $"(파일:{_muscleCount} / 아바타:{pose.muscles.Length}) " +
                                 "— 가능한 범위만 복사합니다.");
            }

            Array.Copy(_muscles[CurrentFrame], pose.muscles, copyLen);

            if (overrideBodyTransform)
            {
                pose.bodyPosition = _positions[CurrentFrame];
                pose.bodyRotation = _rotations[CurrentFrame];
            }
            // overrideBodyTransform = false 시 bodyPosition/Rotation은
            // Animator가 제공한 값 그대로 유지 → 아바타가 제자리에 서있음

            return true;
        }

        // ── 컨트롤 ───────────────────────────────────────────────

        public void TogglePlay()
        {
            if (!IsLoaded) return;
            IsPlaying = !IsPlaying;
        }

        public void Rewind()
        {
            CurrentFrame = 0;
            _timer       = 0f;
        }

        // ── 프레임 진행 ───────────────────────────────────────────

        void AdvanceTime()
        {
            _timer += Time.deltaTime * speed;
            float frameDuration = 1f / _fps;

            while (_timer >= frameDuration)
            {
                _timer -= frameDuration;
                CurrentFrame++;

                if (CurrentFrame >= FrameCount)
                {
                    if (loop) CurrentFrame = 0;
                    else
                    {
                        CurrentFrame = FrameCount - 1;
                        IsPlaying    = false;
                    }
                }
            }
        }

        // ── GUI ───────────────────────────────────────────────────

        void OnGUI()
        {
            var style = new GUIStyle(GUI.skin.box) { fontSize = 13, alignment = TextAnchor.UpperLeft };
            style.normal.textColor = Color.white;

            if (!IsLoaded)
            {
                GUI.Box(new Rect(10, 10, 230, 40),
                    "Motion Player: 파일 없음\n" + (string.IsNullOrEmpty(fileName) ? "(fileName 미설정)" : fileName),
                    style);
                return;
            }

            string stateStr = IsPlaying ? "<color=#00ff88>▶ 재생</color>" : "<color=#ffaa00>■ 정지</color>";
            float  curSec   = CurrentFrame / _fps;
            float  totalSec = FrameCount   / _fps;

            // 진행 바 텍스트
            int barLen  = 20;
            int filled  = Mathf.RoundToInt(Progress * barLen);
            string bar  = "[" + new string('█', filled) + new string('─', barLen - filled) + "]";

            string text =
                $"Motion Player  {stateStr}\n" +
                $"{bar}\n" +
                $"{curSec:F1}s / {totalSec:F1}s   ×{speed:F1}\n" +
                $"[Space] 재생/정지   [BS] 되감기";

            GUI.Box(new Rect(10, 10, 260, 80), text, style);
        }
    }
}
