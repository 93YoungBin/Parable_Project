using System;
using System.IO;
using UnityEngine;

namespace Parable.Motion
{
    /// <summary>
    /// .pmocap 파일을 읽어 HumanoidPoseData로 변환 후 파이프라인에 공급.
    ///
    /// 용도:
    ///   SyntheticMotionBaker로 구운 노이즈 포함 데이터를
    ///   두 아바타(Raw / Filtered)가 동일하게 재생 →
    ///   같은 입력에 Cleanup 유무만 다른 순수 비교 가능.
    ///
    /// 조작:
    ///   [Space] 재생 / 정지
    ///   [R]     처음으로 되감기
    /// </summary>
    public class PmocapMotionSource : MonoBehaviour
    {
        [Header("파일")]
        [Tooltip("Recordings/ 폴더 기준 파일명 (예: synthetic_arms_noisy.pmocap)")]
        public string fileName = "synthetic_arms_noisy.pmocap";

        [Header("재생")]
        public bool playOnStart = true;
        public bool loop        = true;
        [Range(0.1f, 3f)] public float speed = 1f;

        [Header("파이프라인 연결")]
        public HumanoidPipelineStage nextStage;

        [Header("마스크")]
        [Tooltip("Arms = 팔 근육만 적용 (척추/다리는 idle 유지)")]
        public PoseMaskFlags activeMask = PoseMaskFlags.Arms;

        // ── 공개 상태 ─────────────────────────────────────────────────
        public bool  IsLoaded     { get; private set; }
        public bool  IsPlaying    { get; private set; }
        public int   FrameCount   { get; private set; }
        public int   CurrentFrame { get; private set; }
        public float Progress     => FrameCount > 0 ? (float)CurrentFrame / FrameCount : 0f;

        // ── 내부 데이터 ───────────────────────────────────────────────
        float[][]    _muscles;
        Vector3[]    _positions;
        Quaternion[] _rotations;
        int          _muscleCount;
        float        _fps = 30f;
        float        _timer;

        [System.NonSerialized] HumanoidPoseData _pose;

        // ── 생명주기 ──────────────────────────────────────────────────

        void Awake()
        {
            _pose = new HumanoidPoseData();
        }

        void Start()
        {
            if (!string.IsNullOrEmpty(fileName))
                Load(fileName);
        }

        void LateUpdate()
        {
            if (Input.GetKeyDown(KeyCode.Space)) TogglePlay();
            if (Input.GetKeyDown(KeyCode.R))     Rewind();

            if (!IsLoaded || !IsPlaying) return;

            AdvanceTime();
            FillPose();
            nextStage?.Receive(_pose);
        }

        // ── 로드 ──────────────────────────────────────────────────────

        public void Load(string file)
        {
            string path = Path.GetFullPath(
                Path.Combine(Application.dataPath, "..", "Recordings", file));

            if (!File.Exists(path))
            {
                Debug.LogError($"[PmocapMotionSource] 파일 없음: {path}");
                return;
            }

            try   { LoadBinary(path); }
            catch (Exception e) { Debug.LogError($"[PmocapMotionSource] 로드 실패: {e.Message}"); }
        }

        void LoadBinary(string path)
        {
            using var r = new BinaryReader(File.Open(path, FileMode.Open));

            string magic = new string(r.ReadChars(8)).TrimEnd('\0');
            if (magic != "PMOCAP")
            {
                Debug.LogError("[PmocapMotionSource] 잘못된 파일 형식");
                return;
            }

            r.ReadInt32();               // version
            _fps         = r.ReadSingle();
            FrameCount   = r.ReadInt32();
            _muscleCount = r.ReadInt32();

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

            Debug.Log($"[PmocapMotionSource] 로드 완료 — {FrameCount}프레임 / {FrameCount / _fps:F1}초 / {_fps}fps\n{path}");

            if (playOnStart) IsPlaying = true;
        }

        // ── 포즈 채우기 ───────────────────────────────────────────────

        void FillPose()
        {
            int copyLen = Mathf.Min(_pose.muscles.Length, _muscleCount);
            Array.Copy(_muscles[CurrentFrame], _pose.muscles, copyLen);
            _pose.bodyPosition = _positions[CurrentFrame];
            _pose.bodyRotation = _rotations[CurrentFrame];
            _pose.timestamp    = Time.time;
            _pose.isValid      = true;
            _pose.activeMask   = activeMask;
        }

        // ── 프레임 진행 ───────────────────────────────────────────────

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
                    else { CurrentFrame = FrameCount - 1; IsPlaying = false; }
                }
            }
        }

        // ── 컨트롤 ───────────────────────────────────────────────────

        public void TogglePlay() { if (IsLoaded) IsPlaying = !IsPlaying; }
        public void Rewind()     { CurrentFrame = 0; _timer = 0f; }

        // ── GUI ───────────────────────────────────────────────────────

        void OnGUI()
        {
            if (!IsLoaded) return;

            var style = new GUIStyle(GUI.skin.box) { fontSize = 12 };
            style.normal.textColor = Color.white;

            string state = IsPlaying ? "▶" : "■";
            int barLen   = 20;
            int filled   = Mathf.RoundToInt(Progress * barLen);
            string bar   = "[" + new string('█', filled) + new string('─', barLen - filled) + "]";

            GUI.Box(new Rect(10, 10, 240, 55),
                $"Pmocap  {state}  {bar}\n" +
                $"{CurrentFrame}/{FrameCount}  [Space]재생  [R]되감기", style);
        }
    }
}
